using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.SatoshiTickets.Models.Api;

public class CreatePurchaseRequest
{
    [Required]
    [MinLength(1)]
    public PurchaseTicketItemRequest[] Tickets { get; set; }
    public string RedirectUrl { get; set; }
}
