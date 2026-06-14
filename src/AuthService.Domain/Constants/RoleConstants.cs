namespace AuthService.Domain.Constants;

public class RoleConstants
{
    public const string MASTER_ADMIN = "MASTER_ADMIN";
    public const string ADMIN_ROL = "ADMIN";
    public const string USER_ROL = "USER";

    public static readonly string[] AllowedRoles = 
    { 
        MASTER_ADMIN, 
        ADMIN_ROL, 
        USER_ROL 
    };
}