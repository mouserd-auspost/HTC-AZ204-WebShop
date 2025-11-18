using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Contoso.Api.Data;
using Contoso.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Contoso.Api.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly ContosoDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthenticationService(ContosoDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<LoginDto> LoginAsync(UserCredentialsDto userLoginDto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userLoginDto.Email); 
    
        var isValidUser = VerifyPasswordHash(userLoginDto.Password, Convert.FromBase64String(user.PasswordHash));
    
        Console.WriteLine("isValidUser: " + isValidUser);
    
        if (!isValidUser || user == null)
        {
            return null;
        }
    
        var token = await CreateToken(user);

        return new LoginDto
        {
            Token = token,
            UserName = user.Name ?? "User",
        };
    }

    public async Task<LoginDto> RegisterAsync(UserDto userDto)
    {
        var isUserExists = await _context.Users
                            .Where(u => u.Email == userDto.Email)
                            .CountAsync() > 0;

        if (isUserExists)
        {
            return null;
        }

        var passwordHash = HashPassword(userDto.Password);


        var newUser = new User
        {
            Email = userDto.Email,
            PasswordHash = passwordHash,
            Name = userDto.Name,
            Address = userDto.Address,
            CreatedAt = DateTime.Now,
        };

        _context.Users.Add(newUser);

        await _context.SaveChangesAsync();

        var token = await CreateToken(newUser);

        return new LoginDto
        {
            Token = token,
            UserName = newUser.Name ?? "User",
        };  
    }




    private string HashPassword(string password)
    {
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_configuration["JwtSettings:Key"])))
        {
            var passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(passwordHash);
        }
    }

    private bool VerifyPasswordHash(string password, byte[] passwordHash)
    {
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_configuration["JwtSettings:Key"])))
        {
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            for (int i = 0; i < computedHash.Length; i++)
            {
                if (computedHash[i] != passwordHash[i])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private async Task<string> CreateToken(User user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:Key"]));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
        };

        var token = new JwtSecurityToken(
            _configuration["JwtSettings:Issuer"],
            _configuration["JwtSettings:Audience"],
            claims,
            expires: DateTime.Now.AddMinutes(Convert.ToInt32(_configuration["JwtSettings:DurationInMinutes"])),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    
}