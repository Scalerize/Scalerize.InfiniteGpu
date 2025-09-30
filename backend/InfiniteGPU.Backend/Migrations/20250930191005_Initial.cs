using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfiniteGPU.Backend.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ResourceCapabilities = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Balance = table.Column<decimal>(type: "decimal(18,6)", nullable: false, defaultValue: 0m),
                    StripeConnectedAccountId = table.Column<string>(type: "nvarchar(255)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(2)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKeys_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceIdentifier = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsConnected = table.Column<bool>(type: "bit", nullable: false),
                    LastConnectionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    LastConnectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastDisconnectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_AspNetUsers_ProviderUserId",
                        column: x => x.ProviderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    StripeId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SettledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderModelCaches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastAccessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AccessCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderModelCaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderModelCaches_AspNetUsers_ProviderUserId",
                        column: x => x.ProviderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Settlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BankAccountDetails = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    StripeTransferId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Settlements_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    OnnxModelBlobUri = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FillBindingsViaApi = table.Column<bool>(type: "bit", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastProgressAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastHeartbeatAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletionPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tasks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subtasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Progress = table.Column<int>(type: "int", nullable: false),
                    Params = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExecutionSpecJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OnnxModelBlobUri = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ExecutionStateJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationSeconds = table.Column<double>(type: "float", nullable: true),
                    CostUsd = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    LastHeartbeatAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextHeartbeatDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastCommandAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    FailedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequiresReassignment = table.Column<bool>(type: "bit", nullable: false),
                    ReassignmentRequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subtasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subtasks_AspNetUsers_ProviderUserId",
                        column: x => x.ProviderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subtasks_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subtasks_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskInferenceBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TensorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PayloadType = table.Column<int>(type: "int", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskInferenceBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskInferenceBindings_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskOutputBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TensorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PayloadType = table.Column<int>(type: "int", nullable: false),
                    FileFormat = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskOutputBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskOutputBindings_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Earnings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubtaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Earnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Earnings_AspNetUsers_ProviderUserId",
                        column: x => x.ProviderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Earnings_Subtasks_SubtaskId",
                        column: x => x.SubtaskId,
                        principalTable: "Subtasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Earnings_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubtaskTimelineEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubtaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubtaskTimelineEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubtaskTimelineEvents_Subtasks_SubtaskId",
                        column: x => x.SubtaskId,
                        principalTable: "Subtasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Withdrawals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestorUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubtaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SettledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Withdrawals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Withdrawals_AspNetUsers_RequestorUserId",
                        column: x => x.RequestorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Withdrawals_Subtasks_SubtaskId",
                        column: x => x.SubtaskId,
                        principalTable: "Subtasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Withdrawals_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_Key",
                table: "ApiKeys",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_UserId_Prefix",
                table: "ApiKeys",
                columns: new[] { "UserId", "Prefix" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_IsActive",
                table: "AspNetUsers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceIdentifier",
                table: "Devices",
                column: "DeviceIdentifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ProviderUserId",
                table: "Devices",
                column: "ProviderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Earnings_CreatedAtUtc",
                table: "Earnings",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Earnings_ProviderUserId",
                table: "Earnings",
                column: "ProviderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Earnings_Status",
                table: "Earnings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Earnings_SubtaskId",
                table: "Earnings",
                column: "SubtaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Earnings_TaskId",
                table: "Earnings",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CreatedAtUtc",
                table: "Payments",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status",
                table: "Payments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_StripeId",
                table: "Payments",
                column: "StripeId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_UserId",
                table: "Payments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderModelCaches_ProviderUserId_StoredFileName",
                table: "ProviderModelCaches",
                columns: new[] { "ProviderUserId", "StoredFileName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_CreatedAtUtc",
                table: "Settlements",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_Status",
                table: "Settlements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_UserId",
                table: "Settlements",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Subtasks_DeviceId",
                table: "Subtasks",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Subtasks_NextHeartbeatDueAtUtc",
                table: "Subtasks",
                column: "NextHeartbeatDueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Subtasks_ProviderUserId",
                table: "Subtasks",
                column: "ProviderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Subtasks_RequiresReassignment",
                table: "Subtasks",
                column: "RequiresReassignment");

            migrationBuilder.CreateIndex(
                name: "IX_Subtasks_Status",
                table: "Subtasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Subtasks_TaskId",
                table: "Subtasks",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_SubtaskTimelineEvents_EventType",
                table: "SubtaskTimelineEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_SubtaskTimelineEvents_SubtaskId",
                table: "SubtaskTimelineEvents",
                column: "SubtaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskInferenceBindings_TaskId",
                table: "TaskInferenceBindings",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskOutputBindings_TaskId",
                table: "TaskOutputBindings",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_LastHeartbeatAtUtc",
                table: "Tasks",
                column: "LastHeartbeatAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_LastProgressAtUtc",
                table: "Tasks",
                column: "LastProgressAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_OnnxModelBlobUri",
                table: "Tasks",
                column: "OnnxModelBlobUri");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Status",
                table: "Tasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_UserId",
                table: "Tasks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Withdrawals_CreatedAtUtc",
                table: "Withdrawals",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Withdrawals_RequestorUserId",
                table: "Withdrawals",
                column: "RequestorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Withdrawals_Status",
                table: "Withdrawals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Withdrawals_SubtaskId",
                table: "Withdrawals",
                column: "SubtaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Withdrawals_TaskId",
                table: "Withdrawals",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Earnings");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "ProviderModelCaches");

            migrationBuilder.DropTable(
                name: "Settlements");

            migrationBuilder.DropTable(
                name: "SubtaskTimelineEvents");

            migrationBuilder.DropTable(
                name: "TaskInferenceBindings");

            migrationBuilder.DropTable(
                name: "TaskOutputBindings");

            migrationBuilder.DropTable(
                name: "Withdrawals");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Subtasks");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Tasks");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
