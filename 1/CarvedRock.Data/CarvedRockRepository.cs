using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CarvedRock.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CarvedRock.Data
{
    public class CarvedRockRepository : ICarvedRockRepository
    {
        private readonly LocalContext _ctx;
        private readonly ILogger<CarvedRockRepository> _logger;
        private readonly ILogger _factoryLogger;
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache _distributedCache;

        public CarvedRockRepository(LocalContext ctx, ILogger<CarvedRockRepository> logger,
            ILoggerFactory loggerFactory, IMemoryCache memoryCache, IDistributedCache distributedCache)
        {
            _ctx = ctx;
            _logger = logger;
            _factoryLogger = loggerFactory.CreateLogger("DataAccessLayer");
            _memoryCache = memoryCache;
            _distributedCache = distributedCache;
        }

        public async Task<List<Product>> GetProductsAsync(string category)
        {
            _logger.LogInformation("Getting products in repository for {category}", category);
            if (category == "clothing")
            {
                var ex = new ApplicationException("Database error occurred!!");
                ex.Data.Add("Category", category);
                throw ex;
            }
            if (category == "equip")
            {
                throw new SqliteException("Simulated fatal database error occurred!", 551);
            }

            try
            {
                var cacheKey = $"products_{category}";

                //if (!_memoryCache.TryGetValue(cacheKey, out List<Product> results))
                //{
                //    results = await _ctx.Products
                //        .Where(p => p.Category == category || category == "all")
                //        .Include(p => p.Rating)
                //        .ToListAsync();

                //    _memoryCache.Set(cacheKey, results, TimeSpan.FromMinutes(2));
                //}

                var distResults = await _distributedCache.GetAsync(cacheKey);
                if (distResults is null)
                {
                    var prodcutsToSerialize = await _ctx.Products
                       .Where(p => p.Category == category || category == "all")
                       .Include(p => p.Rating)
                       .ToListAsync();

                    var serialized = JsonSerializer.Serialize(prodcutsToSerialize, CacheSourceGeneratorContext.Default.ListProduct);

                    await _distributedCache.SetAsync(cacheKey, Encoding.UTF8.GetBytes
                        (serialized), new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
                        });

                    return prodcutsToSerialize;
                }

                var results = JsonSerializer.Deserialize(Encoding.UTF8.GetString(distResults), CacheSourceGeneratorContext.Default.ListProduct);

                return results ?? new List<Product>();
            }
            catch (Exception ex)
            {
                var newEx = new ApplicationException("Something bad happened in database", ex);
                newEx.Data.Add("Category", category);
                throw newEx;
            }
        }

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            return await _ctx.Products.FindAsync(id);
        }

        public List<Product> GetProducts(string category)
        {
            return _ctx.Products.Where(p => p.Category == category || category == "all").ToList();
        }

        public Product? GetProductById(int id)
        {
            var timer = new Stopwatch();
            timer.Start();

            var product = _ctx.Products.Find(id);
            timer.Stop();

            _logger.LogDebug("Querying products for {id} finished in {milliseconds} milliseconds",
                id, timer.ElapsedMilliseconds);

            _factoryLogger.LogInformation("(F) Querying products for {id} finished in {ticks} ticks",
                id, timer.ElapsedTicks);

            return product;
        }
    }
}