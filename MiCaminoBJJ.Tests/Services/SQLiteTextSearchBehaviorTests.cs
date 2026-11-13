using System.Text;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace MiCaminoBJJ.Tests.Services;

/// <summary>
/// Fase A — Verificación empírica del comportamiento de SQLite en memoria
/// con los textos tal como los almacena EF Core (TEXT UTF-8, sin normalizar).
///
/// Objetivo: decidir la estrategia de búsqueda sin suposiciones sobre
/// NOCASE, LIKE, tildes, ñ y caracteres Unicode.
/// NO modifica la base de datos real: todo corre contra "DataSource=:memory:".
/// </summary>
public class SQLiteTextSearchBehaviorTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public SQLiteTextSearchBehaviorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Misma forma que usa la app: columna Name TEXT (NFC tal cual se teclea/almacena).
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Techniques (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        Insert("América");
        Insert("niño");
        Insert("Llave Americana");
        Insert("triángulo inverso");
        Insert("Kimura");
    }

    private void Insert(string name)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Techniques (Name) VALUES ($name);";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.ExecuteNonQuery();
    }

    private List<string> Query(string sql, string? param = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        if (param != null)
            cmd.Parameters.AddWithValue("$p", param);

        var results = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            results.Add(reader.GetString(0));
        return results;
    }

    public void Dispose() => _connection.Dispose();

    // ── NOCASE ──────────────────────────────────────────────────────────────

    [Fact]
    public void Nocase_IgnoresAsciiCase()
    {
        // NOCASE dobla solo A-Z (ASCII).
        var r = Query("SELECT 'americana' = 'AMERICANA' COLLATE NOCASE");
        r.Should().ContainSingle().Which.Should().Be("1");
    }

    [Fact]
    public void Nocase_DoesNotFoldNonAsciiCase()
    {
        // 'É' vs 'é' NO son iguales con NOCASE: es ASCII-only.
        var r = Query("SELECT 'AMÉRICA' = 'américa' COLLATE NOCASE");
        r.Should().ContainSingle().Which.Should().Be("0");
    }

    [Fact]
    public void Nocase_DoesNotFoldENye()
    {
        // 'Ñ' vs 'ñ': NOCASE tampoco los dobla.
        var r = Query("SELECT 'NIÑO' = 'niño' COLLATE NOCASE");
        r.Should().ContainSingle().Which.Should().Be("0");
    }

    [Fact]
    public void Nocase_DoesNotMatchENyeAgainstN()
    {
        // ñ y n son caracteres distintos en UTF-8: NOCASE no los colapsa.
        var r = Query("SELECT 'NIÑO' = 'NINO' COLLATE NOCASE");
        r.Should().ContainSingle().Which.Should().Be("0");
    }

    // ── LIKE ────────────────────────────────────────────────────────────────

    [Fact]
    public void Like_IsCaseInsensitiveForAsciiOnly()
    {
        var r = Query("SELECT count(*) FROM Techniques WHERE Name LIKE '%LLAVE%'");
        r.Should().ContainSingle().Which.Should().Be("1");
    }

    [Fact]
    public void Like_DoesNotIgnoreAccents()
    {
        // Buscar 'america' sin tilde NO encuentra 'América' con tilde: LIKE es byte-wise
        // salvo el plegado de caso ASCII.
        var r = Query("SELECT count(*) FROM Techniques WHERE Name LIKE '%america%'");
        r.Should().ContainSingle().Which.Should().Be("1"); // solo "Llave Americana"
    }

    [Fact]
    public void Like_AccentedCaseIsCaseSensitive()
    {
        // 'AMÉRICA' vs stored 'América': la É no se pliega → sin match.
        var r = Query("SELECT count(*) FROM Techniques WHERE Name LIKE '%AMÉRICA%'");
        r.Should().ContainSingle().Which.Should().Be("0");
    }

    [Fact]
    public void Like_AccentedLowerCaseMatchesExactly()
    {
        var r = Query("SELECT count(*) FROM Techniques WHERE Name LIKE '%érica%'");
        r.Should().ContainSingle().Which.Should().Be("1");
    }

    [Fact]
    public void Like_WithNocaseColumn_StillFailsOnAccents()
    {
        // Aunque la columna fuera COLLATE NOCASE, la tilde sigue sin ignorarse.
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM Techniques WHERE Name COLLATE NOCASE LIKE '%AMERICA%'";
        var r = ((long)cmd.ExecuteScalar()!).ToString();
        r.Should().Be("1"); // solo "Llave Americana" (América no matchea)
    }

    // ── Ñ y Unicode ─────────────────────────────────────────────────────────

    [Fact]
    public void Like_ENyeRequiresExactMatch()
    {
        var r = Query("SELECT count(*) FROM Techniques WHERE Name LIKE '%niño%'");
        r.Should().ContainSingle().Which.Should().Be("1");

        var r2 = Query("SELECT count(*) FROM Techniques WHERE Name LIKE '%nino%'");
        r2.Should().ContainSingle().Which.Should().Be("0");
    }

    [Fact]
    public void StoredText_IsComposedNfc()
    {
        // Verifica que EF/Microsoft.Data.Sqlite almacena el texto compuesto (NFC):
        // "ñ" es 1 char, no 'n' + U+0303.
        var stored = Query("SELECT Name FROM Techniques WHERE Name LIKE '%niño%'").Single();
        stored.Normalize(NormalizationForm.FormC).Should().Be(stored);
        stored.Should().HaveLength(4); // n-i-ñ-o
    }

    [Fact]
    public void Like_FailsAgainstDecomposedNfdTerm()
    {
        // Si el término buscado viniera descompuesto (NFD), LIKE no matchearía NFC.
        var nfdNino = "nino\u0303"; // n + i + n + tilde combinante (descompuesto)
        var r = Query("SELECT count(*) FROM Techniques WHERE Name LIKE '%' || $p || '%'", nfdNino);
        r.Should().ContainSingle().Which.Should().Be("0");
    }

    // ── Estrategia normalizada (ambas orillas) ──────────────────────────────

    [Fact]
    public void NormalizedColumn_MatchesRegardlessOfAccentsAndCase()
    {
        // Columna normalizada con la MISMA regla de TextNormalizer (a persistir en 2.2).
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "ALTER TABLE Techniques ADD COLUMN NameNormalized TEXT;";
            cmd.ExecuteNonQuery();
        }

        foreach (var row in Query("SELECT Id || '|' || Name FROM Techniques"))
        {
            var parts = row.Split('|', 2);
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "UPDATE Techniques SET NameNormalized = $n WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$n", Core.Services.TextNormalizer.Normalize(parts[1]));
            cmd.Parameters.AddWithValue("$id", int.Parse(parts[0]));
            cmd.ExecuteNonQuery();
        }

        // Tildes ignoradas en AMBAS orillas:
        Query("SELECT count(*) FROM Techniques WHERE NameNormalized LIKE '%' || $p || '%'",
                Core.Services.TextNormalizer.Normalize("america"))
            .Should().ContainSingle().Which.Should().Be("2"); // "América" + "Llave Americana"

        // Ñ tolerante (ñ↔n):
        Query("SELECT count(*) FROM Techniques WHERE NameNormalized LIKE '%' || $p || '%'",
                Core.Services.TextNormalizer.Normalize("nino"))
            .Should().ContainSingle().Which.Should().Be("1");

        // Mayúsculas/mixtas:
        Query("SELECT count(*) FROM Techniques WHERE NameNormalized LIKE '%' || $p || '%'",
                Core.Services.TextNormalizer.Normalize("TRIÁNGULO Inverso"))
            .Should().ContainSingle().Which.Should().Be("1");

        // Término vacío no debe usarse (ver contrato del servicio en Fase B).
        Core.Services.TextNormalizer.Normalize("   ").Should().BeEmpty();
    }

    // ── Índices y LIKE con comodín inicial ──────────────────────────────────

    [Fact]
    public void LeadingWildcard_LikeDoesNotUseBtreeIndex()
    {
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "ALTER TABLE Techniques ADD COLUMN NameNormalized TEXT; " +
                              "UPDATE Techniques SET NameNormalized = Name; " +
                              "CREATE INDEX IX_Techniques_NameNormalized ON Techniques(NameNormalized);";
            cmd.ExecuteNonQuery();
        }

        var plan = QueryDetail("""
            EXPLAIN QUERY PLAN
            SELECT * FROM Techniques WHERE NameNormalized LIKE '%america%'
            """);

        // Un LIKE '%texto%' NUNCA aprovecha el índice B-tree (no hay prefijo).
        plan.Should().NotContain("USING INDEX", plan);
        plan.Should().Contain("SCAN");
    }

    [Fact]
    public void PrefixLike_BinaryIndex_IsNotUsed()
    {
        // Hallazgo: LIKE es case-insensitive (ASCII), pero un índice BINARY por defecto
        // NO puede usarse para optimizarlo. SQLite exige índice COLLATE NOCASE.
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "CREATE INDEX IX_Techniques_Name ON Techniques(Name);";
            cmd.ExecuteNonQuery();
        }

        var plan = QueryDetail("""
            EXPLAIN QUERY PLAN
            SELECT * FROM Techniques WHERE Name LIKE 'Kim%'
            """);

        plan.Should().Contain("SCAN");
    }

    [Fact]
    public void PrefixLike_NocaseIndex_IsUsed()
    {
        // Con índice COLLATE NOCASE el LIKE con prefijo sí usa el índice (solo ASCII).
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "CREATE INDEX IX_Techniques_Name_Nocase ON Techniques(Name COLLATE NOCASE);";
            cmd.ExecuteNonQuery();
        }

        var plan = QueryDetail("""
            EXPLAIN QUERY PLAN
            SELECT * FROM Techniques WHERE Name LIKE 'Kim%'
            """);

        plan.Should().Match(p => p.Contains("USING INDEX") || p.Contains("USING COVERING INDEX"), plan);
    }

    [Fact]
    public void SubstringLike_EvenWithNocaseIndex_Scans()
    {
        // '%texto%' jamás usa índice B-tree: confirma que la búsqueda por subcadena
        // es un scan — aceptable por volumen (catálogo de cientos/t miles de técnicas),
        // y el único camino a escala mayor es FTS5 (si está disponible).
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "ALTER TABLE Techniques ADD COLUMN NameNormalized TEXT; " +
                              "UPDATE Techniques SET NameNormalized = Name; " +
                              "CREATE INDEX IX_Techniques_NameNormalized ON Techniques(NameNormalized COLLATE NOCASE);";
            cmd.ExecuteNonQuery();
        }

        var plan = QueryDetail("""
            EXPLAIN QUERY PLAN
            SELECT * FROM Techniques WHERE NameNormalized LIKE '%ERIC%'
            """);

        plan.Should().Contain("SCAN");
    }

    /// <summary>EXPLAIN QUERY PLAN: columnas (id, parent, notused, detail) → devolvemos detail.</summary>
    private string QueryDetail(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var details = new List<string>();
        while (reader.Read())
            details.Add(reader.GetString(3));
        return string.Join(" | ", details);
    }

    [Fact]
    public void Fts5_IsAvailable()
    {
        // Si FTS5 está disponible queda como opción futura de escala; si no, el plan
        // se mantiene en LIKE + índice de cobertura.
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "CREATE VIRTUAL TABLE FtsProbe USING fts5(Name);";
            cmd.ExecuteNonQuery();
            true.Should().BeTrue(); // disponible
        }
        catch (SqliteException ex)
        {
            ex.SqliteErrorCode.Should().Be(1); // "no such module: fts5" → no disponible
        }
    }
}
