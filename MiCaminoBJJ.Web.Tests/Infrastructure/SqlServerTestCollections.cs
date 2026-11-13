using Xunit;

namespace MiCaminoBJJ.Web.Tests.Infrastructure;

/// <summary>
/// Base aislada con el catálogo de la biblioteca sembrado (solo lectura).
/// Nombre de base ÚNICO por ejecución (ver <see cref="TestDatabaseLease"/>).
/// </summary>
public sealed class CatalogoAisladoFixture : BaseAisladaFixture
{
    public CatalogoAisladoFixture() : base("Catalogo", sembrar: true)
    {
    }
}

/// <summary>
/// Base aislada EXCLUSIVA de las pruebas administrativas: comparte esquema y
/// catálogo, pero no los conteos exactos de la clase de solo lectura.
/// </summary>
public sealed class AdminAisladoFixture : BaseAisladaFixture
{
    public AdminAisladoFixture() : base("Admin", sembrar: true)
    {
    }
}

/// <summary>
/// Colección secuencial de las pruebas de catálogo (evita paralelismo entre las
/// clases que comparten la misma base aislada y los conteos exactos).
/// </summary>
[CollectionDefinition("SqlServerLocalDb")]
public sealed class SqlServerLocalDbCollection : ICollectionFixture<CatalogoAisladoFixture>
{
}

/// <summary>Colección aislada de las pruebas administrativas.</summary>
[CollectionDefinition("SqlServerLocalDbAdmin")]
public sealed class SqlServerLocalDbAdminCollection : ICollectionFixture<AdminAisladoFixture>
{
}

/// <summary>
/// Serializa las pruebas de cuenta (registro/login): comparten el esquema de
/// Identity y no deben correr en paralelo con las del pipeline. No aporta
/// fixture propio: cada clase gestiona su base aislada única.
/// </summary>
[CollectionDefinition("SqlServerLocalDbCuenta")]
public sealed class SqlServerLocalDbCuentaCollection
{
}

/// <summary>
/// Serializa las pruebas del pipeline HTTP real: cada fábrica levanta su propia
/// base aislada única, pero el host en memoria (variables de entorno y
/// servicios) no debe ejecutarse en paralelo con otras pruebas de integración.
/// </summary>
[CollectionDefinition("SqlServerLocalDbPipeline")]
public sealed class SqlServerLocalDbPipelineCollection
{
}

/// <summary>
/// Serializa las pruebas de bootstrap/Identity que tocan SQL Server (esquema
/// propio) para que no compitan con el resto de las integraciones.
/// </summary>
[CollectionDefinition("SqlServerLocalDbSeguridad")]
public sealed class SqlServerLocalDbSeguridadCollection
{
}

