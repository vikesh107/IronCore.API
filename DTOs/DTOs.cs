using IronCore.API.Models;
using System.ComponentModel.DataAnnotations;

namespace IronCore.API.DTOs;

// ── Auth ─────────────────────────────────────────────────────────────────────
public record LoginRequest(
    [Required][EmailAddress] string Email,
    [Required] string Password
);

public record RegisterOwnerRequest(
    [Required][MaxLength(100)] string FullName,
    [Required][EmailAddress] string Email,
    [Required][MinLength(8)] string Password,
    [Required][MaxLength(100)] string GymName,
    string? GymAddress,
    string? GymPhone
);

public record AcceptInviteRequest(
    [Required] string Token,
    [Required][MaxLength(100)] string FullName,
    [Required][MinLength(8)] string Password,
    string? Phone
);

public record ForgotPasswordRequest([Required][EmailAddress] string Email);
public record ResetPasswordRequest(
    [Required] string Token,
    [Required][EmailAddress] string Email,
    [Required][MinLength(8)] string NewPassword
);
public record RefreshTokenRequest([Required] string RefreshToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    string UserId,
    string FullName,
    string Email,
    string Role,
    string GymId,
    DateTime ExpiresAt
);

// ── Direct Create ─────────────────────────────────────────────────────────────
public record CreateTrainerRequest(
    [Required][MaxLength(100)] string FullName,
    [Required][EmailAddress] string Email,
    [Required][MinLength(8)] string Password,
    [MaxLength(100)] string? Specialty,
    [MaxLength(500)] string? Bio
);

public record CreateMemberRequest(
    [Required][MaxLength(100)] string FullName,
    [Required][EmailAddress] string Email,
    [Required][MinLength(8)] string Password,
    string? Phone
);

public record RegisterMemberRequest(
    [Required][MaxLength(100)] string FullName,
    [Required][EmailAddress] string Email,
    [Required][MinLength(8)] string Password,
    string? Phone,
    string? GymId  // optional — auto-resolves to the single gym if omitted
);

// ── Invite (kept for compatibility) ──────────────────────────────────────────
public record InviteRequest(
    [Required][EmailAddress] string Email,
    [Required] string Role  // "Trainer" or "Member"
);

public record InviteInfoResponse(
    string Email,
    string Role,
    string GymName
);

// ── Trainer ───────────────────────────────────────────────────────────────────
public record TrainerDto(
    string Id,
    string UserId,
    string FullName,
    string Email,
    string Specialty,
    string? Bio,
    int ClientCount,
    DateTime JoinedAt,
    bool IsActive
);

public record UpdateTrainerRequest(
    [MaxLength(100)] string? Specialty,
    [MaxLength(500)] string? Bio
);

public record UpdatePasswordRequest([Required][MinLength(8)] string NewPassword);

// ── Member ────────────────────────────────────────────────────────────────────
public record MemberDto(
    string Id,
    string UserId,
    string FullName,
    string Email,
    string? Phone,
    string? TrainerId,
    string? TrainerName,
    SubscriptionDto? ActiveSubscription,
    DateTime JoinedAt,
    bool IsActive
);

public record AssignTrainerRequest([Required] string MemberId, string? TrainerId);

public record UpdateMemberRequest(
    string? Phone,
    string? Gender,
    DateTime? DateOfBirth
);

// ── Subscription ──────────────────────────────────────────────────────────────
public record SubscriptionDto(
    string Id,
    string PlanType,
    string Status,
    DateTime StartDate,
    DateTime EndDate,
    decimal Amount,
    int DaysLeft,
    string? Notes
);

public record CreateSubscriptionRequest(
    [Required] string MemberId,
    [Required] string PlanType,  // Monthly/Quarterly/Yearly
    [Required] DateTime StartDate,
    decimal? Amount,
    string? Notes
);

public record UpdateSubscriptionStatusRequest(
    [Required] string Status  // Active/Expired/Cancelled
);

public record UpdateSubscriptionDatesRequest(
    [Required] DateTime StartDate,
    [Required] DateTime EndDate
);

// ── Attendance ────────────────────────────────────────────────────────────────
public record AttendanceLogDto(
    string Id,
    string MemberId,
    string MemberName,
    DateOnly Date,
    bool IsPresent,
    string? Notes
);

public record CreateAttendanceLogRequest(
    [Required] string MemberId,
    [Required] DateOnly Date,
    bool IsPresent,
    [MaxLength(500)] string? Notes
);

// ── Dashboard ─────────────────────────────────────────────────────────────────
public record OwnerDashboardDto(
    int TotalMembers,
    int ActiveMembers,
    int ExpiredMembers,
    int TotalTrainers,
    int UnassignedMembers,
    int ExpiringThisWeek,
    decimal EstimatedMonthlyRevenue,
    List<TrainerLoadDto> TrainerLoads,
    List<RecentActivityDto> RecentActivities
);

public record TrainerLoadDto(
    string TrainerId,
    string TrainerName,
    int ClientCount,
    int ActiveClients
);

public record RecentActivityDto(
    string Type,
    string Message,
    DateTime At
);

public record TrainerDashboardDto(
    int TotalClients,
    int ActiveClients,
    int ExpiringThisWeek,
    List<MemberDto> Clients
);

public record MemberDashboardDto(
    MemberDto Profile,
    SubscriptionDto? ActiveSubscription,
    List<SubscriptionDto> History
);
