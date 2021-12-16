using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Benchmark.Specification.Benchmarks;

[MemoryDiagnoser]
public class SpecIncludeBenchmark
{
    private readonly int max = 100000;
    private readonly SpecificationEvaluator evaluator = SpecificationEvaluator.Default;
    private readonly SpecificationEvaluator evaluatorCached = new SpecificationEvaluator(new IEvaluator[]
    {
        WhereEvaluator.Instance,
        Ardalis.Specification.EntityFrameworkCore.SearchEvaluator.Instance,
        IncludeEvaluatorCached.Instance,
        OrderEvaluator.Instance,
        PaginationEvaluator.Instance,
        AsNoTrackingEvaluator.Instance,
#if NETSTANDARD2_1
                AsSplitQueryEvaluator.Instance,
                AsNoTrackingWithIdentityResolutionEvaluator.Instance
#endif
    });
    private readonly Specification<Store> specInclude = new StoreIncludeProductsSpec();
    private readonly Specification<Store> specIncludeCached = new StoreIncludeProductsSpecCached();

    private readonly IQueryable<Store> Stores;

    public SpecIncludeBenchmark()
    {
        Stores = new BenchmarkDbContext().Stores.AsQueryable();
    }

    [Benchmark]
    public void EFIncludeExpression()
    {
        for (int i = 0; i < max; i++)
        {
            _ = Stores.Include(x => x.Products).ThenInclude(x => x.CustomFields);
        }
    }

    [Benchmark]
    public void SpecIncludeExpression()
    {
        for (int i = 0; i < max; i++)
        {
            _ = evaluator.GetQuery(Stores, specInclude);
        }
    }

    [Benchmark]
    public void SpecIncludeExpressionCached()
    {
        for (int i = 0; i < max; i++)
        {
            _ = evaluatorCached.GetQuery(Stores, specIncludeCached);
        }
    }
}

public class BenchmarkDbContext : DbContext
{
    public DbSet<Store> Stores { get; set; }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Integrated Security=SSPI;Initial Catalog=SpecificationEFTestsDB;ConnectRetryCount=0");
    }
}

public class StoreIncludeProductsSpec : Specification<Store>
{
    public StoreIncludeProductsSpec()
    {
        Query.Include(x => x.Products).ThenInclude(x => x.CustomFields);
    }
}

public class StoreIncludeProductsSpecCached : Specification<Store>
{
    public StoreIncludeProductsSpecCached()
    {
        Query.Include(x => x.Products).ThenIncludeCached(x => x.CustomFields);
    }
}

public class Store
{
    public int Id { get; set; }
    public IEnumerable<Product> Products { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public CustomFields CustomFields { get; set; }
}

public class CustomFields
{
    public int Id { get; set; }
    public string CustomText1 { get; set; }
    public string CustomText2 { get; set; }
}