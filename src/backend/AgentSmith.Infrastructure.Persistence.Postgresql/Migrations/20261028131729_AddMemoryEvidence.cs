using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Postgresql.Migrations
{
    /// <inheritdoc />
    public partial class AddMemoryEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActiveRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Project = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    JobId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovedSeries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tracker = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketKey = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    SeriesId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Repositories = table.Column<string>(type: "text", nullable: false),
                    CarryingRepo = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedInConversation = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    SatisfiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovedSeries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatRunBindings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Platform = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    ChannelId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    ThreadId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    RequestedBy = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    ReplyEndpoint = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    QuestionId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    QuestionJson = table.Column<string>(type: "text", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRunBindings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClosedTickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tracker = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketKey = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    By = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    ClosedPullRequestsJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    TrackerClosed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosedTickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfigEntities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityType = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Doc = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigEntities", x => x.Id);
                    table.UniqueConstraint("AK_ConfigEntities_EntityType_EntityId", x => new { x.EntityType, x.EntityId });
                });

            migrationBuilder.CreateTable(
                name: "ConfigEntityVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityType = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Doc = table.Column<string>(type: "text", nullable: true),
                    ChangedBy = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigEntityVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConnectionDiscoveries",
                columns: table => new
                {
                    ConnectionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReposJson = table.Column<string>(type: "text", nullable: true),
                    DiscoveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionDiscoveries", x => x.ConnectionName);
                });

            migrationBuilder.CreateTable(
                name: "DialogueAnswers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DialogueJobId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    QuestionId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    AnsweredBy = table.Column<string>(type: "text", nullable: false),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogueAnswers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LandedTickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tracker = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketKey = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LandedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    By = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    PrUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandedTickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemoryEvidence",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RepoKey = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Slug = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    Project = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    SpecId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CriterionKey = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryEvidence", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ObservedCallers",
                columns: table => new
                {
                    Subject = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    NameClaim = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    NameValue = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    RoleValues = table.Column<string>(type: "text", nullable: false),
                    GroupValues = table.Column<string>(type: "text", nullable: false),
                    GroupsOmitted = table.Column<bool>(type: "boolean", nullable: false),
                    FirstSeen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObservedCallers", x => x.Subject);
                });

            migrationBuilder.CreateTable(
                name: "PendingClarifications",
                columns: table => new
                {
                    Platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ChannelId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    SuggestedText = table.Column<string>(type: "text", nullable: false),
                    OriginalInput = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Token = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingClarifications", x => new { x.Platform, x.ChannelId });
                });

            migrationBuilder.CreateTable(
                name: "PrReviewEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PrKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    HostId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Decision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Standing = table.Column<bool>(type: "boolean", nullable: false),
                    AuthorLogin = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    Trusted = table.Column<bool>(type: "boolean", nullable: false),
                    IsOurs = table.Column<bool>(type: "boolean", nullable: false),
                    IsBot = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    File = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Line = table.Column<int>(type: "integer", nullable: true),
                    Resolved = table.Column<bool>(type: "boolean", nullable: true),
                    ThreadHostId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Truncated = table.Column<bool>(type: "boolean", nullable: false),
                    AtTicks = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrReviewEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrReviewRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PrKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Repo = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    RepoKey = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Number = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StateAtTicks = table.Column<long>(type: "bigint", nullable: true),
                    CheckedAtTicks = table.Column<long>(type: "bigint", nullable: false),
                    HeadSha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    HeadMovedAtTicks = table.Column<long>(type: "bigint", nullable: true),
                    ReopenedAtTicks = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrReviewRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrSweepRepositories",
                columns: table => new
                {
                    Repository = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Initialised = table.Column<bool>(type: "boolean", nullable: false),
                    LastSweptTicks = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrSweepRepositories", x => x.Repository);
                });

            migrationBuilder.CreateTable(
                name: "PrSweepStates",
                columns: table => new
                {
                    Repository = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Number = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    LabelPresent = table.Column<bool>(type: "boolean", nullable: false),
                    CommentsSeenTicks = table.Column<long>(type: "bigint", nullable: false),
                    CommentsSeenId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrSweepStates", x => new { x.Repository, x.Number });
                });

            migrationBuilder.CreateTable(
                name: "QueuedTickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Project = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Pipeline = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Platform = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    ReservedRunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    EnqueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InitialContextJson = table.Column<string>(type: "text", nullable: true),
                    PlanAnswersJson = table.Column<string>(type: "text", nullable: true),
                    IsResume = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueuedTickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceFiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'1000000000', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    SetId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RelativePath = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    MediaType = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReworkLedgers",
                columns: table => new
                {
                    Project = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    NotServedThroughTicks = table.Column<long>(type: "bigint", nullable: true),
                    SpokenKeys = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReworkLedgers", x => new { x.Project, x.TicketId });
                });

            migrationBuilder.CreateTable(
                name: "ReworkNudges",
                columns: table => new
                {
                    Project = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    PrUrl = table.Column<string>(type: "text", nullable: true),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    DueTicks = table.Column<long>(type: "bigint", nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    ClaimToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Tries = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReworkNudges", x => new { x.Project, x.TicketId });
                });

            migrationBuilder.CreateTable(
                name: "RunArtifacts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunArtifacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    CauseChannel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CauseActor = table.Column<string>(type: "text", nullable: true),
                    CauseAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SourceKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceUrl = table.Column<string>(type: "text", nullable: true),
                    SourceId = table.Column<string>(type: "text", nullable: true),
                    SourceDetail = table.Column<string>(type: "text", nullable: true),
                    TicketFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TrackerState = table.Column<string>(type: "text", nullable: true),
                    WebUrl = table.Column<string>(type: "text", nullable: true),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: true),
                    RevisionCause = table.Column<string>(type: "text", nullable: true),
                    SeriesId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    CarriedSpecIdsJson = table.Column<string>(type: "text", nullable: true),
                    ApprovalAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovalConversation = table.Column<string>(type: "text", nullable: true),
                    ApprovalPrincipal = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunCapacities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    FootprintJson = table.Column<string>(type: "text", nullable: false),
                    TotalCpuNanos = table.Column<long>(type: "bigint", nullable: false),
                    TotalMemBytes = table.Column<long>(type: "bigint", nullable: false),
                    Reserved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunCapacities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunCheckpoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Platform = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    Pipeline = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    DialogueJobId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    QuestionId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    QuestionJson = table.Column<string>(type: "text", nullable: false),
                    RemainingCommandsJson = table.Column<string>(type: "text", nullable: false),
                    ContextJson = table.Column<string>(type: "text", nullable: false),
                    ExecutionCount = table.Column<int>(type: "integer", nullable: false),
                    AskedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AnswerDeadlineAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunConversationEntry",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    QuestionId = table.Column<string>(type: "text", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: true),
                    DetailJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunConversationEntry", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunCriterionJudgements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    CriterionKey = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    CriterionText = table.Column<string>(type: "text", nullable: false),
                    MachineStatus = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    HumanStatus = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Author = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunCriterionJudgements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunDecisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: true),
                    StepIndex = table.Column<int>(type: "integer", nullable: true),
                    SpecId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunDecisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunDefaultDecisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    SpecId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    Question = table.Column<string>(type: "text", nullable: false),
                    Chosen = table.Column<string>(type: "text", nullable: false),
                    AlternativesJson = table.Column<string>(type: "text", nullable: false),
                    Why = table.Column<string>(type: "text", nullable: false),
                    HoldsSeries = table.Column<bool>(type: "boolean", nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PointerPath = table.Column<string>(type: "text", nullable: true),
                    TakenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Verdict = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "pending"),
                    VerdictBy = table.Column<string>(type: "text", nullable: true),
                    VerdictAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VerdictNote = table.Column<string>(type: "text", nullable: true),
                    Conversation = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunDefaultDecisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Seq = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: true),
                    Phase = table.Column<string>(type: "text", nullable: true),
                    Repo = table.Column<string>(type: "text", nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    StepIndex = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunExpectations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    DraftJson = table.Column<string>(type: "text", nullable: false),
                    RatifiedJson = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    RatifiedBy = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    RatifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EditDistance = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunExpectations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunLedgerTransitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    SpecId = table.Column<string>(type: "text", nullable: true),
                    MasterPass = table.Column<int>(type: "integer", nullable: true),
                    TransitionsJson = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunLedgerTransitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunLlmCalls",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: true),
                    Phase = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "text", nullable: true),
                    TokensIn = table.Column<long>(type: "bigint", nullable: false),
                    TokensOut = table.Column<long>(type: "bigint", nullable: false),
                    CostUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    PromptHash = table.Column<string>(type: "text", nullable: true),
                    CachedTokensIn = table.Column<long>(type: "bigint", nullable: false),
                    CacheCreationTokensIn = table.Column<long>(type: "bigint", nullable: false),
                    StepIndex = table.Column<int>(type: "integer", nullable: true),
                    SpecId = table.Column<string>(type: "text", nullable: true),
                    MasterPass = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunLlmCalls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunPasses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    SpecId = table.Column<string>(type: "text", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    PassInStep = table.Column<int>(type: "integer", nullable: false),
                    OpenedBecause = table.Column<string>(type: "text", nullable: false),
                    EndedBecause = table.Column<string>(type: "text", nullable: false),
                    ReengageReason = table.Column<string>(type: "text", nullable: true),
                    VerdictEmitted = table.Column<bool>(type: "boolean", nullable: false),
                    CriteriaMet = table.Column<int>(type: "integer", nullable: true),
                    CriteriaTotal = table.Column<int>(type: "integer", nullable: true),
                    CriteriaJson = table.Column<string>(type: "text", nullable: true),
                    GateLine = table.Column<string>(type: "text", nullable: false),
                    ActionablePending = table.Column<int>(type: "integer", nullable: false),
                    UnbackedDone = table.Column<int>(type: "integer", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunPasses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunRepos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    RepoName = table.Column<string>(type: "text", nullable: false),
                    PrUrl = table.Column<string>(type: "text", nullable: true),
                    PrStatus = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    ChangeCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunRepos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunRetryRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunRetryRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Runs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Pipeline = table.Column<string>(type: "text", nullable: false),
                    TicketId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketTitle = table.Column<string>(type: "text", nullable: true),
                    Platform = table.Column<string>(type: "text", nullable: true),
                    Trigger = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RepoMode = table.Column<string>(type: "text", nullable: true),
                    AgentName = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActsReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TotalSteps = table.Column<int>(type: "integer", nullable: true),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    EndKind = table.Column<string>(type: "text", nullable: true),
                    CostTotalUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    TokensIn = table.Column<long>(type: "bigint", nullable: false),
                    TokensOut = table.Column<long>(type: "bigint", nullable: false),
                    CancelRequested = table.Column<bool>(type: "boolean", nullable: false),
                    CancelReason = table.Column<string>(type: "text", nullable: true),
                    JobId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    CancelDeadlineAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    QueuedRequestJson = table.Column<string>(type: "text", nullable: true),
                    RequestEnqueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProgressLedgerJson = table.Column<string>(type: "text", nullable: true),
                    AcceptanceJson = table.Column<string>(type: "text", nullable: true),
                    PullRequestsJson = table.Column<string>(type: "text", nullable: true),
                    BudgetTier = table.Column<string>(type: "text", nullable: true),
                    BudgetCapUsd = table.Column<decimal>(type: "numeric", nullable: true),
                    BudgetCapTokens = table.Column<long>(type: "bigint", nullable: true),
                    WorkShape = table.Column<string>(type: "text", nullable: true),
                    WorkShapeReason = table.Column<string>(type: "text", nullable: true),
                    RunMetricsJson = table.Column<string>(type: "text", nullable: true),
                    PlannedStepsJson = table.Column<string>(type: "text", nullable: true),
                    PlannedFirstStepIndex = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunSandboxes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    RepoName = table.Column<string>(type: "text", nullable: true),
                    ToolchainImage = table.Column<string>(type: "text", nullable: true),
                    EndReason = table.Column<string>(type: "text", nullable: true),
                    ExitCode = table.Column<int>(type: "integer", nullable: true),
                    OomKilled = table.Column<bool>(type: "boolean", nullable: true),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    SpawnedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DisposedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MemoryRequest = table.Column<string>(type: "text", nullable: true),
                    StepIndex = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSandboxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunSegments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndSeq = table.Column<long>(type: "bigint", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndStatus = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSegments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunSeriesCuts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    SetKey = table.Column<string>(type: "text", nullable: false),
                    HandbackCase = table.Column<string>(type: "text", nullable: true),
                    SpecsJson = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSeriesCuts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunSpecReviewRounds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    SpecId = table.Column<string>(type: "text", nullable: true),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    Reviewer = table.Column<int>(type: "integer", nullable: false),
                    Verdict = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CutsJson = table.Column<string>(type: "text", nullable: false),
                    FindingsJson = table.Column<string>(type: "text", nullable: false),
                    EvidenceProblemsJson = table.Column<string>(type: "text", nullable: false),
                    ResiduesJson = table.Column<string>(type: "text", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSpecReviewRounds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunSpecs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    SpecId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Verdict = table.Column<string>(type: "text", nullable: true),
                    AcceptanceJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSpecs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunSteps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    EventSeq = table.Column<long>(type: "bigint", nullable: true),
                    StepIndex = table.Column<int>(type: "integer", nullable: false),
                    StepName = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    CommandName = table.Column<string>(type: "text", nullable: true),
                    SpecId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    ResultMessage = table.Column<string>(type: "text", nullable: true),
                    LlmMs = table.Column<long>(type: "bigint", nullable: false),
                    ThrottleWaitMs = table.Column<long>(type: "bigint", nullable: false),
                    SandboxMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSteps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpecDialogAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    MediaType = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    ContentBase64 = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecDialogAttachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpecDialogSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Platform = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    ChannelId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    ThreadId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    UserId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    ReposJson = table.Column<string>(type: "text", nullable: false),
                    TranscriptJson = table.Column<string>(type: "text", nullable: false),
                    ConfirmedOutcomeJson = table.Column<string>(type: "text", nullable: true),
                    LatestProposalJson = table.Column<string>(type: "text", nullable: true),
                    LatestFilingJson = table.Column<string>(type: "text", nullable: true),
                    Subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Tracker = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    TicketKey = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    IsOpen = table.Column<bool>(type: "boolean", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecDialogSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpecDialogTicketText",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Text = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Truncated = table.Column<bool>(type: "boolean", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecDialogTicketText", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SweepCursors",
                columns: table => new
                {
                    Source = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    AtTicks = table.Column<long>(type: "bigint", nullable: false),
                    Resume = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SweepCursors", x => x.Source);
                });

            migrationBuilder.CreateTable(
                name: "TakenTickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Project = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Platform = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Pipeline = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TakenTickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketBetweenAttemptsChecks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FingerprintUnchanged = table.Column<bool>(type: "boolean", nullable: false),
                    ActsJson = table.Column<string>(type: "text", nullable: false),
                    ClosedForWork = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketBetweenAttemptsChecks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketRequirements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tracker = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketKey = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Project = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    RequiredTicketKey = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    RequiredTicketId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    StartsWhenMet = table.Column<bool>(type: "boolean", nullable: false),
                    FiledInConversation = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketRequirements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketSeries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Project = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketKey = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    SeriesId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    CarryingRepo = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    RevisionSha = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    LastHandbackCase = table.Column<int>(type: "integer", nullable: false),
                    RepeatedHandbackCount = table.Column<int>(type: "integer", nullable: false),
                    ExecutedThrough = table.Column<int>(type: "integer", nullable: false),
                    HandbackHeads = table.Column<string>(type: "text", nullable: true),
                    RevisionShasJson = table.Column<string>(type: "text", nullable: true),
                    ExecutedIdsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSeries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnmovedTickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Project = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    TicketId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Tracker = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    ConfiguredStatus = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    TrackerConfigVersion = table.Column<int>(type: "integer", nullable: false),
                    ProjectConfigVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnmovedTickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookLastSeen",
                columns: table => new
                {
                    Platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookLastSeen", x => x.Platform);
                });

            migrationBuilder.CreateTable(
                name: "ConfigRefs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FromType = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    FromId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    ToType = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    ToId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigRefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfigRefs_ConfigEntities_ToType_ToId",
                        columns: x => new { x.ToType, x.ToId },
                        principalTable: "ConfigEntities",
                        principalColumns: new[] { "EntityType", "EntityId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveRuns_Project_TicketId",
                table: "ActiveRuns",
                columns: new[] { "Project", "TicketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSeries_Project",
                table: "ApprovedSeries",
                column: "Project");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSeries_Tracker_SatisfiedAt",
                table: "ApprovedSeries",
                columns: new[] { "Tracker", "SatisfiedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSeries_Tracker_TicketKey",
                table: "ApprovedSeries",
                columns: new[] { "Tracker", "TicketKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatRunBindings_ClosedAt",
                table: "ChatRunBindings",
                column: "ClosedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRunBindings_Platform_ChannelId_ThreadId",
                table: "ChatRunBindings",
                columns: new[] { "Platform", "ChannelId", "ThreadId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatRunBindings_RunId",
                table: "ChatRunBindings",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClosedTickets_Project",
                table: "ClosedTickets",
                column: "Project");

            migrationBuilder.CreateIndex(
                name: "IX_ClosedTickets_Tracker_TicketKey",
                table: "ClosedTickets",
                columns: new[] { "Tracker", "TicketKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfigEntities_EntityType_EntityId",
                table: "ConfigEntities",
                columns: new[] { "EntityType", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfigEntityVersions_EntityType_EntityId_Version",
                table: "ConfigEntityVersions",
                columns: new[] { "EntityType", "EntityId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigRefs_FromType_FromId",
                table: "ConfigRefs",
                columns: new[] { "FromType", "FromId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigRefs_ToType_ToId",
                table: "ConfigRefs",
                columns: new[] { "ToType", "ToId" });

            migrationBuilder.CreateIndex(
                name: "IX_DialogueAnswers_DialogueJobId_QuestionId",
                table: "DialogueAnswers",
                columns: new[] { "DialogueJobId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandedTickets_Project",
                table: "LandedTickets",
                column: "Project");

            migrationBuilder.CreateIndex(
                name: "IX_LandedTickets_Tracker_TicketKey",
                table: "LandedTickets",
                columns: new[] { "Tracker", "TicketKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEvidence_RepoKey_Slug",
                table: "MemoryEvidence",
                columns: new[] { "RepoKey", "Slug" });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEvidence_RunId_SpecId",
                table: "MemoryEvidence",
                columns: new[] { "RunId", "SpecId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrReviewEntries_PrKey_HostId",
                table: "PrReviewEntries",
                columns: new[] { "PrKey", "HostId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrReviewRecords_PrKey",
                table: "PrReviewRecords",
                column: "PrKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrReviewRecords_Project_TicketId",
                table: "PrReviewRecords",
                columns: new[] { "Project", "TicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrReviewRecords_RepoKey_Number",
                table: "PrReviewRecords",
                columns: new[] { "RepoKey", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_PrReviewRecords_RunId",
                table: "PrReviewRecords",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_QueuedTickets_Project_TicketId",
                table: "QueuedTickets",
                columns: new[] { "Project", "TicketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceFiles_SessionId",
                table: "ReferenceFiles",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceFiles_SetId",
                table: "ReferenceFiles",
                column: "SetId");

            migrationBuilder.CreateIndex(
                name: "IX_ReworkNudges_DueTicks",
                table: "ReworkNudges",
                column: "DueTicks");

            migrationBuilder.CreateIndex(
                name: "IX_RunArtifacts_RunId",
                table: "RunArtifacts",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunAttempts_RunId",
                table: "RunAttempts",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunCapacities_Reserved",
                table: "RunCapacities",
                column: "Reserved");

            migrationBuilder.CreateIndex(
                name: "IX_RunCapacities_RunId",
                table: "RunCapacities",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunCheckpoints_DialogueJobId_QuestionId",
                table: "RunCheckpoints",
                columns: new[] { "DialogueJobId", "QuestionId" });

            migrationBuilder.CreateIndex(
                name: "IX_RunCheckpoints_ResumedAt",
                table: "RunCheckpoints",
                column: "ResumedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RunCheckpoints_RunId",
                table: "RunCheckpoints",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunConversationEntry_RunId",
                table: "RunConversationEntry",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunConversationEntry_RunId_EventSeq",
                table: "RunConversationEntry",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunCriterionJudgements_RunId_CriterionKey",
                table: "RunCriterionJudgements",
                columns: new[] { "RunId", "CriterionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunDecisions_RunId",
                table: "RunDecisions",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunDecisions_RunId_EventSeq",
                table: "RunDecisions",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunDefaultDecisions_Conversation",
                table: "RunDefaultDecisions",
                column: "Conversation");

            migrationBuilder.CreateIndex(
                name: "IX_RunDefaultDecisions_RunId",
                table: "RunDefaultDecisions",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunDefaultDecisions_RunId_EventSeq",
                table: "RunDefaultDecisions",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunEvents_RunId",
                table: "RunEvents",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunEvents_RunId_Seq",
                table: "RunEvents",
                columns: new[] { "RunId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunEvents_RunId_StepIndex_Seq",
                table: "RunEvents",
                columns: new[] { "RunId", "StepIndex", "Seq" });

            migrationBuilder.CreateIndex(
                name: "IX_RunExpectations_RunId",
                table: "RunExpectations",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunLedgerTransitions_RunId",
                table: "RunLedgerTransitions",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunLedgerTransitions_RunId_EventSeq",
                table: "RunLedgerTransitions",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunLlmCalls_RunId",
                table: "RunLlmCalls",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunLlmCalls_RunId_EventSeq",
                table: "RunLlmCalls",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunPasses_RunId",
                table: "RunPasses",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunPasses_RunId_EventSeq",
                table: "RunPasses",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunRepos_RunId",
                table: "RunRepos",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunRetryRequests_RunId",
                table: "RunRetryRequests",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Runs_Project",
                table: "Runs",
                column: "Project");

            migrationBuilder.CreateIndex(
                name: "IX_Runs_Project_TicketId",
                table: "Runs",
                columns: new[] { "Project", "TicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_RunSandboxes_RunId",
                table: "RunSandboxes",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunSandboxes_RunId_EventSeq",
                table: "RunSandboxes",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunSegments_RunId",
                table: "RunSegments",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunSegments_RunId_EventSeq",
                table: "RunSegments",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunSeriesCuts_RunId",
                table: "RunSeriesCuts",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunSeriesCuts_RunId_EventSeq",
                table: "RunSeriesCuts",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunSpecReviewRounds_RunId",
                table: "RunSpecReviewRounds",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunSpecReviewRounds_RunId_EventSeq",
                table: "RunSpecReviewRounds",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunSpecs_RunId",
                table: "RunSpecs",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunSpecs_RunId_SpecId",
                table: "RunSpecs",
                columns: new[] { "RunId", "SpecId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunSteps_RunId",
                table: "RunSteps",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RunSteps_RunId_EventSeq",
                table: "RunSteps",
                columns: new[] { "RunId", "EventSeq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogAttachments_SessionId",
                table: "SpecDialogAttachments",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogSessions_Platform_ThreadId",
                table: "SpecDialogSessions",
                columns: new[] { "Platform", "ThreadId" });

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogSessions_SessionId",
                table: "SpecDialogSessions",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogSessions_Tracker_TicketKey",
                table: "SpecDialogSessions",
                columns: new[] { "Tracker", "TicketKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogSessions_UserId",
                table: "SpecDialogSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecDialogTicketText_SessionId",
                table: "SpecDialogTicketText",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TakenTickets_Project_TicketId",
                table: "TakenTickets",
                columns: new[] { "Project", "TicketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketBetweenAttemptsChecks_RunId",
                table: "TicketBetweenAttemptsChecks",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketRequirements_Project",
                table: "TicketRequirements",
                column: "Project");

            migrationBuilder.CreateIndex(
                name: "IX_TicketRequirements_Tracker_RequiredTicketKey",
                table: "TicketRequirements",
                columns: new[] { "Tracker", "RequiredTicketKey" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketRequirements_Tracker_TicketKey_RequiredTicketKey",
                table: "TicketRequirements",
                columns: new[] { "Tracker", "TicketKey", "RequiredTicketKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketSeries_Project_TicketKey",
                table: "TicketSeries",
                columns: new[] { "Project", "TicketKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnmovedTickets_Project_TicketId",
                table: "UnmovedTickets",
                columns: new[] { "Project", "TicketId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveRuns");

            migrationBuilder.DropTable(
                name: "ApprovedSeries");

            migrationBuilder.DropTable(
                name: "ChatRunBindings");

            migrationBuilder.DropTable(
                name: "ClosedTickets");

            migrationBuilder.DropTable(
                name: "ConfigEntityVersions");

            migrationBuilder.DropTable(
                name: "ConfigRefs");

            migrationBuilder.DropTable(
                name: "ConnectionDiscoveries");

            migrationBuilder.DropTable(
                name: "DialogueAnswers");

            migrationBuilder.DropTable(
                name: "LandedTickets");

            migrationBuilder.DropTable(
                name: "MemoryEvidence");

            migrationBuilder.DropTable(
                name: "ObservedCallers");

            migrationBuilder.DropTable(
                name: "PendingClarifications");

            migrationBuilder.DropTable(
                name: "PrReviewEntries");

            migrationBuilder.DropTable(
                name: "PrReviewRecords");

            migrationBuilder.DropTable(
                name: "PrSweepRepositories");

            migrationBuilder.DropTable(
                name: "PrSweepStates");

            migrationBuilder.DropTable(
                name: "QueuedTickets");

            migrationBuilder.DropTable(
                name: "ReferenceFiles");

            migrationBuilder.DropTable(
                name: "ReworkLedgers");

            migrationBuilder.DropTable(
                name: "ReworkNudges");

            migrationBuilder.DropTable(
                name: "RunArtifacts");

            migrationBuilder.DropTable(
                name: "RunAttempts");

            migrationBuilder.DropTable(
                name: "RunCapacities");

            migrationBuilder.DropTable(
                name: "RunCheckpoints");

            migrationBuilder.DropTable(
                name: "RunConversationEntry");

            migrationBuilder.DropTable(
                name: "RunCriterionJudgements");

            migrationBuilder.DropTable(
                name: "RunDecisions");

            migrationBuilder.DropTable(
                name: "RunDefaultDecisions");

            migrationBuilder.DropTable(
                name: "RunEvents");

            migrationBuilder.DropTable(
                name: "RunExpectations");

            migrationBuilder.DropTable(
                name: "RunLedgerTransitions");

            migrationBuilder.DropTable(
                name: "RunLlmCalls");

            migrationBuilder.DropTable(
                name: "RunPasses");

            migrationBuilder.DropTable(
                name: "RunRepos");

            migrationBuilder.DropTable(
                name: "RunRetryRequests");

            migrationBuilder.DropTable(
                name: "Runs");

            migrationBuilder.DropTable(
                name: "RunSandboxes");

            migrationBuilder.DropTable(
                name: "RunSegments");

            migrationBuilder.DropTable(
                name: "RunSeriesCuts");

            migrationBuilder.DropTable(
                name: "RunSpecReviewRounds");

            migrationBuilder.DropTable(
                name: "RunSpecs");

            migrationBuilder.DropTable(
                name: "RunSteps");

            migrationBuilder.DropTable(
                name: "SpecDialogAttachments");

            migrationBuilder.DropTable(
                name: "SpecDialogSessions");

            migrationBuilder.DropTable(
                name: "SpecDialogTicketText");

            migrationBuilder.DropTable(
                name: "SweepCursors");

            migrationBuilder.DropTable(
                name: "TakenTickets");

            migrationBuilder.DropTable(
                name: "TicketBetweenAttemptsChecks");

            migrationBuilder.DropTable(
                name: "TicketRequirements");

            migrationBuilder.DropTable(
                name: "TicketSeries");

            migrationBuilder.DropTable(
                name: "UnmovedTickets");

            migrationBuilder.DropTable(
                name: "WebhookLastSeen");

            migrationBuilder.DropTable(
                name: "ConfigEntities");
        }
    }
}
