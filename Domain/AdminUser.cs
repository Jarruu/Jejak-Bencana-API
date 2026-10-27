using Microsoft.AspNetCore.Identity;

namespace JejakBencana.Api.Domain;

/// <summary>Akun admin. Kata sandi dikelola ASP.NET Core Identity (hash + salt), tidak pernah disimpan sebagai teks.</summary>
public class AdminUser : IdentityUser
{
}
