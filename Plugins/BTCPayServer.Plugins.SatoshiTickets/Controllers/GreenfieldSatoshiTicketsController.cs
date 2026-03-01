using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Controllers;
using BTCPayServer.Data;
using BTCPayServer.Plugins.Emails.Services;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Plugins.SatoshiTickets.Models.Api;
using BTCPayServer.Plugins.SatoshiTickets.Services;
using BTCPayServer.Services.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.SatoshiTickets.Controllers;

[Route("~/api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/")]
[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield, Policy = Policies.CanModifyStoreSettings)]
[EnableCors(CorsPolicies.All)]
public class GreenfieldSatoshiTicketsController : ControllerBase
{
    private readonly TicketService _ticketService;
    private readonly EmailService _emailService;
    private readonly EmailSenderFactory _emailSenderFactory;
    private readonly SimpleTicketSalesDbContextFactory _dbContextFactory;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly UIInvoiceController _invoiceController;
    private readonly LinkGenerator _linkGenerator;

    public GreenfieldSatoshiTicketsController(
        TicketService ticketService,
        EmailService emailService,
        EmailSenderFactory emailSenderFactory,
        SimpleTicketSalesDbContextFactory dbContextFactory,
        InvoiceRepository invoiceRepository,
        UIInvoiceController invoiceController,
        LinkGenerator linkGenerator)
    {
        _ticketService = ticketService;
        _emailService = emailService;
        _emailSenderFactory = emailSenderFactory;
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _invoiceController = invoiceController;
        _linkGenerator = linkGenerator;
    }

    private string CurrentStoreId => HttpContext.GetStoreData()?.Id;

    [HttpGet("tickets")]
    public async Task<IActionResult> GetTickets(string storeId, string eventId, [FromQuery] string searchText = null)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var eventExists = ctx.Events.Any(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (!eventExists)
            return EventNotFound();

        var query = ctx.Tickets.AsNoTracking().Where(t => t.EventId == eventId && t.StoreId == CurrentStoreId
                        && t.PaymentStatus == TransactionStatus.Settled.ToString());

        if (!string.IsNullOrEmpty(searchText))
        {
            searchText = searchText.Trim();
            query = query.Where(t =>
                t.TxnNumber.Contains(searchText) || t.FirstName.Contains(searchText) ||
                t.LastName.Contains(searchText) || t.Email.Contains(searchText) ||t.TicketNumber.Contains(searchText));
        }
        var tickets = query.ToList();
        var result = tickets.Select(ToTicketData).ToArray();
        return Ok(result);
    }


    [HttpGet("tickets/export")]
    public async Task<IActionResult> ExportTickets(string storeId, string eventId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (ticketEvent == null)
            return EventNotFound();

        var ordersWithTickets = ctx.Orders.AsNoTracking()
            .Where(o => o.StoreId == CurrentStoreId && o.EventId == eventId
                        && o.PaymentStatus == TransactionStatus.Settled.ToString())
            .SelectMany(o => o.Tickets.Select(t => new
            {
                o.PurchaseDate,
                t.TxnNumber,
                t.FirstName,
                t.LastName,
                t.Email,
                t.TicketTypeName,
                t.Amount,
                o.Currency,
                t.UsedAt
            })).ToList();

        if (!ordersWithTickets.Any())
            return this.CreateAPIError(404, "no-tickets", "No settled tickets found for this event");

        var fileName = $"{ticketEvent.Title}_Tickets-{DateTime.UtcNow:yyyy_MM_dd-HH_mm_ss}.csv";
        var csvData = new StringBuilder();
        csvData.AppendLine("Purchase Date,Ticket Number,First Name,Last Name,Email,Ticket Tier,Amount,Currency,Attended Event");
        foreach (var ticket in ordersWithTickets)
        {
            csvData.AppendLine(string.Join(",",
                EscapeCsvField(ticket.PurchaseDate?.ToString("MM/dd/yy HH:mm")),
                EscapeCsvField(ticket.TxnNumber),
                EscapeCsvField(ticket.FirstName),
                EscapeCsvField(ticket.LastName),
                EscapeCsvField(ticket.Email),
                EscapeCsvField(ticket.TicketTypeName),
                EscapeCsvField(ticket.Amount.ToString()),
                EscapeCsvField(ticket.Currency),
                EscapeCsvField(ticket.UsedAt.HasValue.ToString())));
        }

        byte[] fileBytes = Encoding.UTF8.GetBytes(csvData.ToString());
        return File(fileBytes, "text/csv", fileName);
    }


    [HttpPost("tickets/{ticketNumber}/check-in")]
    public async Task<IActionResult> CheckinTicket(string storeId, string eventId, string ticketNumber)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ticketExist = ctx.Events.Any(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (!ticketExist)
            return EventNotFound();

        var checkinResult = await _ticketService.CheckinTicket(eventId, ticketNumber, CurrentStoreId);
        if (!checkinResult.Success)
            return this.CreateAPIError(422, "checkin-failed", checkinResult.ErrorMessage);

        var result = new CheckinResultData
        {
            Success = checkinResult.Success,
            ErrorMessage = checkinResult.ErrorMessage,
            Ticket = checkinResult.Ticket != null ? ToTicketData(checkinResult.Ticket) : null
        };
        return Ok(result);
    }


    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(string storeId, string eventId, [FromQuery] string searchText = null)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ticketExist = ctx.Events.Any(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (!ticketExist)
            return EventNotFound();

        var query = ctx.Orders.AsNoTracking().Include(c => c.Tickets)
            .Where(c => c.EventId == eventId && c.StoreId == CurrentStoreId && c.PaymentStatus == TransactionStatus.Settled.ToString());

        if (!string.IsNullOrEmpty(searchText))
        {
            query = query.Where(o =>
                o.InvoiceId.Contains(searchText) ||
                o.Tickets.Any(t =>
                    t.TxnNumber.Contains(searchText) ||
                    t.FirstName.Contains(searchText) ||
                    t.LastName.Contains(searchText) ||
                    t.Email.Contains(searchText)));
        }

        var orders = query.ToList();
        var result = orders.Select(ToOrderData).ToArray();
        return Ok(result);
    }

    [HttpPost("purchase")]
    public async Task<IActionResult> CreatePurchase(string storeId, string eventId, [FromBody] CreatePurchaseRequest request)
    {
        if (request?.Tickets == null || request.Tickets.Length == 0)
            return this.CreateAPIError(422, "validation-error", "At least one ticket item is required");

        var store = HttpContext.GetStoreData();
        if (store == null || store.Id != storeId)
            return this.CreateAPIError(404, "store-not-found", "The store was not found");

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (ticketEvent == null)
            return EventNotFound();

        var now = DateTime.UtcNow;
        if (ticketEvent.EventState == Data.EntityState.Disabled)
            return this.CreateAPIError(422, "event-not-active", "The event is not active");
        if (ticketEvent.StartDate.Date < now.Date)
            return this.CreateAPIError(422, "event-expired", "The event has already started or ended");
        if (ticketEvent.EndDate.HasValue && ticketEvent.EndDate.Value.Date < now.Date)
            return this.CreateAPIError(422, "event-expired", "The event has ended");

        if (ticketEvent.HasMaximumCapacity && ticketEvent.MaximumEventCapacity.HasValue)
        {
            var totalTicketsSold = ctx.Orders.AsNoTracking()
                .Where(c => c.StoreId == storeId && c.EventId == eventId && c.PaymentStatus == TransactionStatus.Settled.ToString())
                .SelectMany(c => c.Tickets).Count();
            if (totalTicketsSold >= ticketEvent.MaximumEventCapacity.Value)
                return this.CreateAPIError(422, "event-capacity-reached", "The event has reached maximum capacity");
        }

        var ticketTypes = ctx.TicketTypes.Where(t => t.EventId == eventId).ToDictionary(t => t.Id);

        foreach (var item in request.Tickets)
        {
            if (item.Recipients == null || item.Recipients.Length != item.Quantity)
                return this.CreateAPIError(422, "recipients-count-mismatch",
                    $"Recipients count must equal quantity ({item.Quantity}) for ticket type {item.TicketTypeId}");

            if (!ticketTypes.TryGetValue(item.TicketTypeId, out var ticketType))
                return this.CreateAPIError(404, "ticket-type-not-found",
                    $"Ticket type {item.TicketTypeId} was not found");

            if (ticketType.TicketTypeState == Data.EntityState.Disabled)
                return this.CreateAPIError(422, "ticket-type-not-active",
                    $"Ticket type {ticketType.Name} is not active");

            var available = ticketType.Quantity - ticketType.QuantitySold;
            if (ticketType.Quantity > 0 && available < item.Quantity)
                return this.CreateAPIError(422, "insufficient-quantity",
                    $"Insufficient quantity for ticket type {ticketType.Name}. Available: {available}, requested: {item.Quantity}");

            foreach (var recipient in item.Recipients)
            {
                if (string.IsNullOrWhiteSpace(recipient?.Email))
                    return this.CreateAPIError(422, "invalid-email", "Email is required for each recipient");
            }
        }

        var txnId = Encoders.Base58.EncodeData(RandomUtils.GetBytes(10));
        var orderNow = DateTimeOffset.UtcNow;
        var order = new Order
        {
            TxnId = txnId,
            EventId = eventId,
            StoreId = storeId,
            Currency = ticketEvent.Currency,
            PaymentStatus = TransactionStatus.New.ToString(),
            CreatedAt = orderNow,
            TotalAmount = 0
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var tickets = new List<Ticket>();
        foreach (var item in request.Tickets)
        {
            var ticketType = ticketTypes[item.TicketTypeId];
            for (var i = 0; i < item.Quantity; i++)
            {
                var recipient = item.Recipients[i];
                var ticketTxn = Encoders.Base58.EncodeData(RandomUtils.GetBytes(10));
                var qrCodeLink = Url.Action("EventTicketDisplay", "UITicketSalesPublic",
                    new { storeId, eventId, orderId = order.Id, txnNumber = ticketTxn },
                    Request.Scheme, Request.Host.Value);
                var ticket = new Ticket
                {
                    StoreId = storeId,
                    EventId = eventId,
                    TicketTypeId = ticketType.Id,
                    Amount = ticketType.Price,
                    QRCodeLink = qrCodeLink,
                    FirstName = recipient.FirstName?.Trim() ?? string.Empty,
                    LastName = recipient.LastName?.Trim() ?? string.Empty,
                    Email = recipient.Email?.Trim() ?? string.Empty,
                    CreatedAt = orderNow,
                    TxnNumber = ticketTxn,
                    TicketNumber = $"EVT-{eventId}-{orderNow:yyMMdd}-{ticketTxn}",
                    TicketTypeName = ticketType.Name,
                    PaymentStatus = TransactionStatus.New.ToString()
                };
                tickets.Add(ticket);
            }
        }
        order.Tickets = tickets;
        order.TotalAmount = tickets.Sum(t => t.Amount);
        ctx.Orders.Update(order);
        await ctx.SaveChangesAsync();

        var redirectUrl = !string.IsNullOrEmpty(request.RedirectUrl)
            ? request.RedirectUrl
            : ticketEvent.RedirectUrl ?? string.Empty;
        var invoice = await CreateInvoiceForOrder(store, order, ticketEvent.Currency, redirectUrl);

        order.InvoiceId = invoice.Id;
        order.InvoiceStatus = invoice.Status.ToString();
        ctx.Orders.Update(order);
        await ctx.SaveChangesAsync();

        var checkoutUrl = _linkGenerator.InvoiceCheckoutLink(invoice.Id, Request.GetRequestBaseUrl());
        return StatusCode(201, new PurchaseResponse
        {
            OrderId = order.Id,
            TxnId = order.TxnId,
            InvoiceId = invoice.Id,
            CheckoutUrl = checkoutUrl
        });
    }

    private async Task<BTCPayServer.Services.Invoices.InvoiceEntity> CreateInvoiceForOrder(
        BTCPayServer.Data.StoreData store, Order order, string currency, string redirectUrl)
    {
        var ticketSalesSearchTerm = $"{SimpleTicketSalesHostedService.TICKET_SALES_PREFIX}{order.TxnId}";
        var matchedExistingInvoices = await _invoiceRepository.GetInvoices(new InvoiceQuery
        {
            TextSearch = ticketSalesSearchTerm,
            StoreId = new[] { store.Id }
        });
        matchedExistingInvoices = matchedExistingInvoices
            .Where(entity => entity.GetInternalTags(ticketSalesSearchTerm).Any(s => s == order.TxnId.ToString()))
            .ToArray();

        var settledInvoice = matchedExistingInvoices.LastOrDefault(entity =>
            new[] { "settled", "processing", "confirmed", "paid", "complete" }
                .Contains(entity.GetInvoiceState().Status.ToString().ToLower()));
        if (settledInvoice != null)
            return settledInvoice;

        var invoiceRequest = new BTCPayServer.Client.Models.CreateInvoiceRequest
        {
            Amount = order.TotalAmount,
            Currency = currency,
            Metadata = new JObject
            {
                ["orderId"] = order.Id,
                ["TxnId"] = order.TxnId
            },
            AdditionalSearchTerms = new[]
            {
                order.TxnId.ToString(CultureInfo.InvariantCulture),
                order.Id.ToString(CultureInfo.InvariantCulture),
                ticketSalesSearchTerm
            }
        };
        if (!string.IsNullOrEmpty(redirectUrl))
        {
            invoiceRequest.Checkout = new()
            {
                RedirectURL = redirectUrl
            };
        }
        return await _invoiceController.CreateInvoiceCoreRaw(invoiceRequest, store,
            Request.GetAbsoluteRoot(), new List<string> { ticketSalesSearchTerm });
    }

    [HttpPost("orders/{orderId}/tickets/{ticketId}/send-reminder")]
    public async Task<IActionResult> SendReminder(string storeId, string eventId, string orderId, string ticketId)
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (ticketEvent == null)
            return EventNotFound();

        var order = ctx.Orders.AsNoTracking().Include(c => c.Tickets)
            .FirstOrDefault(o => o.Id == orderId && o.StoreId == CurrentStoreId && o.EventId == eventId && o.Tickets.Any());
        if (order == null)
            return this.CreateAPIError(404, "order-not-found", "The order was not found");

        var ticket = order.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket == null)
            return this.CreateAPIError(404, "ticket-not-found", "The ticket was not found");

        var emailSender = await _emailSenderFactory.GetEmailSender(CurrentStoreId);
        var isEmailConfigured = (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
        if (!isEmailConfigured)
        {
            return this.CreateAPIError(422, "email-not-configured",
                "Email SMTP settings are not configured. Configure email settings in the store admin.");
        }
        try
        {
            var emailResponse = await _emailService.SendTicketRegistrationEmail(CurrentStoreId, ticket, ticketEvent);
            if (emailResponse.IsSuccessful)
            {
                order.EmailSent = true;
                ctx.Orders.Update(order);
                await ctx.SaveChangesAsync();
            }
            else
            {
                var failedList = emailResponse.FailedRecipients?.Count > 0
                    ? string.Join(", ", emailResponse.FailedRecipients) : ticket.Email;
                return this.CreateAPIError(500, "email-send-failed", $"Failed to send ticket email to: {failedList}");
            }
        }
        catch (Exception ex)
        {
            return this.CreateAPIError(500, "email-send-failed", $"An error occurred when sending ticket details: {ex.Message}");
        }
        return Ok(new { success = true, message = "Ticket details have been sent to the recipient via email" });
    }

    private static TicketData ToTicketData(Ticket entity)
    {
        return new TicketData
        {
            Id = entity.Id,
            EventId = entity.EventId,
            TicketTypeId = entity.TicketTypeId,
            TicketTypeName = entity.TicketTypeName,
            Amount = entity.Amount,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            Email = entity.Email,
            TicketNumber = entity.TicketNumber,
            TxnNumber = entity.TxnNumber,
            PaymentStatus = entity.PaymentStatus,
            CheckedIn = entity.UsedAt.HasValue,
            CheckedInAt = entity.UsedAt,
            EmailSent = entity.EmailSent,
            CreatedAt = entity.CreatedAt
        };
    }

    private static OrderData ToOrderData(Order entity)
    {
        return new OrderData
        {
            Id = entity.Id,
            EventId = entity.EventId,
            TotalAmount = entity.TotalAmount,
            Currency = entity.Currency,
            InvoiceId = entity.InvoiceId,
            PaymentStatus = entity.PaymentStatus,
            InvoiceStatus = entity.InvoiceStatus,
            EmailSent = entity.EmailSent,
            CreatedAt = entity.CreatedAt,
            PurchaseDate = entity.PurchaseDate,
            Tickets = entity.Tickets?.Select(ToTicketData).ToList() ?? new()
        };
    }


    private static string EscapeCsvField(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private IActionResult EventNotFound()
    {
        return this.CreateAPIError(404, "event-not-found", "The event was not found");
    }
}
