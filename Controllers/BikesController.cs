using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BikePartsTracker.Data;
using BikePartsTracker.Models;
using BikePartsTracker.DTOs;

namespace BikePartsTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BikesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BikesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Bikes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Bike>>> GetBikes()
        {
            return await _context.Bikes
                .Include(b => b.User)
                .Include(b => b.Parts)
                .ToListAsync();
        }

        // GET: api/Bikes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Bike>> GetBike(Guid id)
        {
            var bike = await _context.Bikes
                .Include(b => b.User)
                .Include(b => b.Parts)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bike == null)
            {
                return NotFound();
            }

            return bike;
        }

        // POST: api/Bikes
        [HttpPost]
        public async Task<ActionResult<Bike>> PostBike(CreateBikeDto createBikeDto)
        {
            // Load the user to satisfy the required navigation property
            var user = await _context.Users.FindAsync(createBikeDto.UserId);
            if (user == null)
            {
                return BadRequest("User not found");
            }

            var bike = new Bike
            {
                Id = Guid.NewGuid(),
                StravaBikeId = createBikeDto.StravaBikeId,
                UserId = createBikeDto.UserId,
                User = user,
                Name = createBikeDto.Name,
                Type = createBikeDto.Type,
                TotalDistance = createBikeDto.TotalDistance,
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Bikes.Add(bike);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBike), new { id = bike.Id }, bike);
        }

        // PUT: api/Bikes/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBike(Guid id, Bike bike)
        {
            if (id != bike.Id)
            {
                return BadRequest();
            }

            // Load the user to satisfy the required navigation property
            var user = await _context.Users.FindAsync(bike.UserId);
            if (user == null)
            {
                return BadRequest("User not found");
            }

            bike.User = user; // Set the navigation property
            _context.Entry(bike).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BikeExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/Bikes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBike(Guid id)
        {
            var bike = await _context.Bikes.FindAsync(id);
            if (bike == null)
            {
                return NotFound();
            }

            _context.Bikes.Remove(bike);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool BikeExists(Guid id)
        {
            return _context.Bikes.Any(e => e.Id == id);
        }
    }
}
