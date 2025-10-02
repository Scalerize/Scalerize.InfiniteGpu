using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace InfiniteGPU.Backend.Data.Entities;

public class ApplicationUser : IdentityUser
{
    [Column(TypeName = "nvarchar(100)")]
    public string? FirstName { get; set; }

    [Column(TypeName = "nvarchar(100)")]
    public string? LastName { get; set; }

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "nvarchar(max)")]
    public string? ResourceCapabilities { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal Balance { get; set; } = 0m;

    [Column(TypeName = "nvarchar(255)")]
    public string? StripeConnectedAccountId { get; set; }

    [Column(TypeName = "nvarchar(2)")]
    public string? Country { get; set; }

    public virtual ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
}