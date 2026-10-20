using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiCaminoBJJ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Achievements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IconPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CriteriaType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CriteriaValue = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsRepeatable = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Achievements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Belts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ImagePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Belts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TechniqueCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechniqueCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PasswordSalt = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    BeltRank = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Academy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Techniques",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Objective = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Steps = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Tips = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    BeltId = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PositionId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Techniques", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Techniques_Belts_BeltId",
                        column: x => x.BeltId,
                        principalTable: "Belts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Techniques_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Techniques_TechniqueCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "TechniqueCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediaAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UploadedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaType = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    LocalPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    RemoteUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ExternalUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ExternalProvider = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalVideoId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    MimeType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ThumbnailPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaAssets_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrainingSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    TrainingType = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Feeling = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAchievements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    AchievementId = table.Column<int>(type: "INTEGER", nullable: false),
                    AwardedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAchievements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAchievements_Achievements_AchievementId",
                        column: x => x.AchievementId,
                        principalTable: "Achievements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAchievements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Language = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    NotificationsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTournaments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Placement = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Division = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTournaments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTournaments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedTechniques",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    TechniqueId = table.Column<int>(type: "INTEGER", nullable: false),
                    SavedType = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedTechniques", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedTechniques_Techniques_TechniqueId",
                        column: x => x.TechniqueId,
                        principalTable: "Techniques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedTechniques_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TechniqueViews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    TechniqueId = table.Column<int>(type: "INTEGER", nullable: false),
                    ViewedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechniqueViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechniqueViews_Techniques_TechniqueId",
                        column: x => x.TechniqueId,
                        principalTable: "Techniques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TechniqueViews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTechniqueProgress",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    TechniqueId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProgressState = table.Column<int>(type: "INTEGER", nullable: false),
                    DominatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTechniqueProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTechniqueProgress_Techniques_TechniqueId",
                        column: x => x.TechniqueId,
                        principalTable: "Techniques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTechniqueProgress_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TechniqueMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TechniqueId = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaAssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCover = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechniqueMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechniqueMedia_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechniqueMedia_Techniques_TechniqueId",
                        column: x => x.TechniqueId,
                        principalTable: "Techniques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProfileMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaAssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsProfilePicture = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfileMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfileMedia_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserProfileMedia_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTechniqueResources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    TechniqueId = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaAssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonalNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTechniqueResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTechniqueResources_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserTechniqueResources_Techniques_TechniqueId",
                        column: x => x.TechniqueId,
                        principalTable: "Techniques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTechniqueResources_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TechniquePractices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    TechniqueId = table.Column<int>(type: "INTEGER", nullable: false),
                    TrainingSessionId = table.Column<int>(type: "INTEGER", nullable: true),
                    PracticedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Repetitions = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    ConfidenceLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechniquePractices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechniquePractices_Techniques_TechniqueId",
                        column: x => x.TechniqueId,
                        principalTable: "Techniques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TechniquePractices_TrainingSessions_TrainingSessionId",
                        column: x => x.TrainingSessionId,
                        principalTable: "TrainingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TechniquePractices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Belts_DisplayOrder",
                table: "Belts",
                column: "DisplayOrder",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_IsDeleted",
                table: "MediaAssets",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_UploadedByUserId",
                table: "MediaAssets",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_DisplayOrder",
                table: "Positions",
                column: "DisplayOrder",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedTechniques_TechniqueId",
                table: "SavedTechniques",
                column: "TechniqueId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedTechniques_UserId_TechniqueId",
                table: "SavedTechniques",
                columns: new[] { "UserId", "TechniqueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechniqueCategories_DisplayOrder",
                table: "TechniqueCategories",
                column: "DisplayOrder",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechniqueMedia_MediaAssetId",
                table: "TechniqueMedia",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_TechniqueMedia_TechniqueId_MediaAssetId",
                table: "TechniqueMedia",
                columns: new[] { "TechniqueId", "MediaAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechniquePractices_PracticedAt",
                table: "TechniquePractices",
                column: "PracticedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TechniquePractices_TechniqueId",
                table: "TechniquePractices",
                column: "TechniqueId");

            migrationBuilder.CreateIndex(
                name: "IX_TechniquePractices_TrainingSessionId",
                table: "TechniquePractices",
                column: "TrainingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TechniquePractices_UserId_TechniqueId",
                table: "TechniquePractices",
                columns: new[] { "UserId", "TechniqueId" });

            migrationBuilder.CreateIndex(
                name: "IX_Techniques_BeltId",
                table: "Techniques",
                column: "BeltId");

            migrationBuilder.CreateIndex(
                name: "IX_Techniques_CategoryId",
                table: "Techniques",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Techniques_IsActive",
                table: "Techniques",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Techniques_PositionId",
                table: "Techniques",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_TechniqueViews_TechniqueId",
                table: "TechniqueViews",
                column: "TechniqueId");

            migrationBuilder.CreateIndex(
                name: "IX_TechniqueViews_UserId_TechniqueId",
                table: "TechniqueViews",
                columns: new[] { "UserId", "TechniqueId" });

            migrationBuilder.CreateIndex(
                name: "IX_TechniqueViews_ViewedAt",
                table: "TechniqueViews",
                column: "ViewedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_Date",
                table: "TrainingSessions",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_UserId",
                table: "TrainingSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_AchievementId",
                table: "UserAchievements",
                column: "AchievementId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_AchievementId",
                table: "UserAchievements",
                columns: new[] { "UserId", "AchievementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfileMedia_MediaAssetId",
                table: "UserProfileMedia",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfileMedia_UserId_IsProfilePicture",
                table: "UserProfileMedia",
                columns: new[] { "UserId", "IsProfilePicture" },
                unique: true,
                filter: "[IsProfilePicture] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_UserId",
                table: "UserSettings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTechniqueProgress_TechniqueId",
                table: "UserTechniqueProgress",
                column: "TechniqueId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTechniqueProgress_UserId_TechniqueId",
                table: "UserTechniqueProgress",
                columns: new[] { "UserId", "TechniqueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTechniqueResources_MediaAssetId",
                table: "UserTechniqueResources",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTechniqueResources_TechniqueId",
                table: "UserTechniqueResources",
                column: "TechniqueId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTechniqueResources_UserId_MediaAssetId",
                table: "UserTechniqueResources",
                columns: new[] { "UserId", "MediaAssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTournaments_UserId",
                table: "UserTournaments",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedTechniques");

            migrationBuilder.DropTable(
                name: "TechniqueMedia");

            migrationBuilder.DropTable(
                name: "TechniquePractices");

            migrationBuilder.DropTable(
                name: "TechniqueViews");

            migrationBuilder.DropTable(
                name: "UserAchievements");

            migrationBuilder.DropTable(
                name: "UserProfileMedia");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "UserTechniqueProgress");

            migrationBuilder.DropTable(
                name: "UserTechniqueResources");

            migrationBuilder.DropTable(
                name: "UserTournaments");

            migrationBuilder.DropTable(
                name: "TrainingSessions");

            migrationBuilder.DropTable(
                name: "Achievements");

            migrationBuilder.DropTable(
                name: "MediaAssets");

            migrationBuilder.DropTable(
                name: "Techniques");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Belts");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "TechniqueCategories");
        }
    }
}
