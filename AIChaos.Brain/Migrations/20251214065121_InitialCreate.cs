using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIChaos.Brain.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreditBalance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalSpent = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    LastRequestTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LinkedYouTubeChannelId = table.Column<string>(type: "TEXT", nullable: true),
                    PictureUrl = table.Column<string>(type: "TEXT", nullable: true),
                    PendingVerificationCode = table.Column<string>(type: "TEXT", nullable: true),
                    VerificationCodeExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SessionToken = table.Column<string>(type: "TEXT", nullable: true),
                    SessionExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    RedoCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingCredits",
                columns: table => new
                {
                    ChannelId = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    PendingBalance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingCredits", x => x.ChannelId);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OpenRouter_ApiKey = table.Column<string>(type: "TEXT", nullable: false),
                    OpenRouter_BaseUrl = table.Column<string>(type: "TEXT", nullable: false),
                    OpenRouter_Model = table.Column<string>(type: "TEXT", nullable: false),
                    Twitch_ClientId = table.Column<string>(type: "TEXT", nullable: false),
                    Twitch_ClientSecret = table.Column<string>(type: "TEXT", nullable: false),
                    Twitch_AccessToken = table.Column<string>(type: "TEXT", nullable: false),
                    Twitch_RefreshToken = table.Column<string>(type: "TEXT", nullable: false),
                    Twitch_Channel = table.Column<string>(type: "TEXT", nullable: false),
                    Twitch_RequireBits = table.Column<bool>(type: "INTEGER", nullable: false),
                    Twitch_MinBitsAmount = table.Column<int>(type: "INTEGER", nullable: false),
                    Twitch_ChatCommand = table.Column<string>(type: "TEXT", nullable: false),
                    Twitch_CooldownSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    Twitch_Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    YouTube_ClientId = table.Column<string>(type: "TEXT", nullable: false),
                    YouTube_ClientSecret = table.Column<string>(type: "TEXT", nullable: false),
                    YouTube_AccessToken = table.Column<string>(type: "TEXT", nullable: false),
                    YouTube_RefreshToken = table.Column<string>(type: "TEXT", nullable: false),
                    YouTube_VideoId = table.Column<string>(type: "TEXT", nullable: false),
                    YouTube_MinSuperChatAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    YouTube_AllowRegularChat = table.Column<bool>(type: "INTEGER", nullable: false),
                    YouTube_ChatCommand = table.Column<string>(type: "TEXT", nullable: false),
                    YouTube_CooldownSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    YouTube_Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    YouTube_AllowViewerOAuth = table.Column<bool>(type: "INTEGER", nullable: false),
                    YouTube_PollingIntervalSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    Safety_BlockUrls = table.Column<bool>(type: "INTEGER", nullable: false),
                    Safety_AllowedDomains = table.Column<string>(type: "TEXT", nullable: false),
                    Safety_Moderators = table.Column<string>(type: "TEXT", nullable: false),
                    Safety_PrivateDiscordMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    Admin_Password = table.Column<string>(type: "TEXT", nullable: false),
                    Tunnel_Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Tunnel_CurrentUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Tunnel_IsRunning = table.Column<bool>(type: "INTEGER", nullable: false),
                    TestClient_Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    TestClient_TestMap = table.Column<string>(type: "TEXT", nullable: false),
                    TestClient_CleanupAfterTest = table.Column<bool>(type: "INTEGER", nullable: false),
                    TestClient_TimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    TestClient_GmodPath = table.Column<string>(type: "TEXT", nullable: false),
                    TestClient_IsConnected = table.Column<bool>(type: "INTEGER", nullable: false),
                    TestClient_LastPollTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    General_StreamMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    General_BlockLinksInGeneratedCode = table.Column<bool>(type: "INTEGER", nullable: false),
                    StreamState_WasStreamLive = table.Column<bool>(type: "INTEGER", nullable: false),
                    StreamState_WasYouTubeListening = table.Column<bool>(type: "INTEGER", nullable: false),
                    StreamState_WasTwitchListening = table.Column<bool>(type: "INTEGER", nullable: false),
                    StreamState_LastYouTubeVideoId = table.Column<string>(type: "TEXT", nullable: true),
                    StreamState_LastTwitchChannel = table.Column<string>(type: "TEXT", nullable: true),
                    StreamState_LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DonationRecords",
                columns: table => new
                {
                    PendingChannelCreditsChannelId = table.Column<string>(type: "TEXT", nullable: false),
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonationRecords", x => new { x.PendingChannelCreditsChannelId, x.Id });
                    table.ForeignKey(
                        name: "FK_DonationRecords_PendingCredits_PendingChannelCreditsChannelId",
                        column: x => x.PendingChannelCreditsChannelId,
                        principalTable: "PendingCredits",
                        principalColumn: "ChannelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_LinkedYouTubeChannelId",
                table: "Accounts",
                column: "LinkedYouTubeChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_SessionToken",
                table: "Accounts",
                column: "SessionToken");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Username",
                table: "Accounts",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "DonationRecords");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "PendingCredits");
        }
    }
}
