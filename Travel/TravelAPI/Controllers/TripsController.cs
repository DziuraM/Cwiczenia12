using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelAPI.Data;
using TravelAPI.DTOs;
using TravelAPI.Models;

namespace TravelAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TripsController : ControllerBase
{
    private readonly TravelContext _context;

    public TripsController(TravelContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetTrips([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = _context.Trips
            .OrderByDescending(t => t.DateFrom)
            .Select(t => new
            {
                t.Name,
                t.Description,
                t.DateFrom,
                t.DateTo,
                t.MaxPeople,
                Countries = t.IdCountries.Select(c => new { c.Name }),
                Clients = t.ClientTrips.Select(ct => new { ct.IdClientNavigation.FirstName, ct.IdClientNavigation.LastName })

            });

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var trips = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new { pageNum = page, pageSize, allPages = totalPages, trips });
    }

    [HttpPost("{idTrip}/clients")]
    public async Task<IActionResult> AssignClientToTrip(int idTrip, AssignClientDto dto)
    {
        var now = DateTime.UtcNow;

        if (await _context.Clients.AnyAsync(c => c.Pesel == dto.Pesel))
            return Conflict("Client with this PESEL already exists.");

        var trip = await _context.Trips.Include(t => t.ClientTrips).FirstOrDefaultAsync(t => t.IdTrip == idTrip);
        if (trip == null || trip.DateFrom <= now)
            return BadRequest("Trip doesn't exist or already started.");

        var client = new Client
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Telephone = dto.Telephone,
            Pesel = dto.Pesel
        };

        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var alreadyAssigned = await _context.ClientTrips.AnyAsync(ct => ct.IdClient == client.IdClient && ct.IdTrip == idTrip);
        if (alreadyAssigned)
            return Conflict("Client already assigned to this trip.");

        _context.ClientTrips.Add(new ClientTrip
        {
            IdClient = client.IdClient,
            IdTrip = idTrip,
            RegisteredAt = now,
            PaymentDate = dto.PaymentDate
        });

        await _context.SaveChangesAsync();

        return Ok("Client assigned to trip.");
    }
}