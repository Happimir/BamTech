using Microsoft.AspNetCore.Identity;

namespace StargateAPI.Business.Data
{
    /// <summary>
    /// An account that can sign in to ACTS. Deliberately separate from <see cref="Person"/>: the
    /// people tracked by the system are not its users, and no person is linked to an account.
    /// </summary>
    public class ApplicationUser : IdentityUser<Guid>
    {
        public ApplicationUser()
        {
            Id = Guid.CreateVersion7();
            SecurityStamp = Guid.NewGuid().ToString();
        }

        /// <summary>A disabled account cannot sign in, and its existing sessions stop working.</summary>
        public bool IsEnabled { get; set; } = true;
    }
}
