using System.Linq.Expressions;
using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Benchmark.Specification.Benchmarks;

[MemoryDiagnoser, MedianColumn, RankColumn]
public class IncludeBenchmark
{
    private Consumer consumer;
    private Context context;
    private IQueryable<Entity> entities;
    private SpecificationEvaluator evaluator;

    [Params(1000, 10000, 100000)]
    public int RepeatCount;

    [GlobalSetup]
    public void GlobalSetup()
    {
        context = new Context(new DbContextOptionsBuilder().UseSqlServer("Server=(localdb)\\mssqllocaldb;Integrated Security=SSPI;Initial Catalog=SpecificationEFTestsDB;ConnectRetryCount=0").Options);
        consumer = new Consumer();
        entities = context.Entities;
        evaluator = new SpecificationEvaluator(new IEvaluator[]
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
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        context.Dispose();
    }

    [Benchmark]
    public object VanillaInclude()
    {
        var query = entities
            .Include(x => x.Prop1).ThenInclude(x => x.Prop1).ThenInclude(x => x.Prop1).ThenInclude(x => x.Prop1).ThenInclude(x => x.Prop1)
            .Include(x => x.Prop2).ThenInclude(x => x.Prop1).ThenInclude(x => x.Prop1);
        for (var i = 0; i < RepeatCount; ++i)
        {
            query = entities
                .Include(x => x.Prop1).ThenInclude(x => x.Prop1).ThenInclude(x => x.Prop1).ThenInclude(x => x.Prop1).ThenInclude(x => x.Prop1)
                .Include(x => x.Prop2).ThenInclude(x => x.Prop1).ThenInclude(x => x.Prop1);
        }

        return query;
    }

    [Benchmark]
    public object SpecificationsInclude()
    {
        Expression<Func<Entity, Entity2>> i1 = x => x.Prop1;
        Expression<Func<Entity2, Entity3>> i2 = x => x.Prop1;
        Expression<Func<Entity3, Entity4>> i3 = x => x.Prop1;
        Expression<Func<Entity4, IEnumerable<Entity5>>> i4 = x => x.Prop1;
        Expression<Func<Entity, Entity4>> i5 = x => x.Prop2;
        Expression<Func<Entity5, Entity6>> i6 = x => x.Prop1;

        var query = entities
            .Include(new IncludeExpressionInfo(i1, typeof(Entity), typeof(Entity2)))
                .ThenInclude(new IncludeExpressionInfo(i2, typeof(Entity), typeof(Entity3), typeof(Entity2)))
                .ThenInclude(new IncludeExpressionInfo(i3, typeof(Entity), typeof(Entity4), typeof(Entity3)))
                .ThenInclude(new IncludeExpressionInfo(i4, typeof(Entity), typeof(IEnumerable<Entity5>), typeof(Entity4)))
                .ThenInclude(new IncludeExpressionInfo(i6, typeof(Entity), typeof(Entity6), typeof(Entity5)))
            .Include(new IncludeExpressionInfo(i5, typeof(Entity), typeof(Entity4)))
                .ThenInclude(new IncludeExpressionInfo(i4, typeof(Entity), typeof(IEnumerable<Entity5>), typeof(Entity4)))
                .ThenInclude(new IncludeExpressionInfo(i6, typeof(Entity), typeof(Entity6), typeof(Entity5)));

        for (var i = 0; i < RepeatCount; ++i)
        {
            query = entities
                .Include(new IncludeExpressionInfo(i1, typeof(Entity), typeof(Entity2)))
                    .ThenInclude(new IncludeExpressionInfo(i2, typeof(Entity), typeof(Entity3), typeof(Entity2)))
                    .ThenInclude(new IncludeExpressionInfo(i3, typeof(Entity), typeof(Entity4), typeof(Entity3)))
                    .ThenInclude(new IncludeExpressionInfo(i4, typeof(Entity), typeof(IEnumerable<Entity5>), typeof(Entity4)))
                    .ThenInclude(new IncludeExpressionInfo(i6, typeof(Entity), typeof(Entity6), typeof(Entity5)))
                .Include(new IncludeExpressionInfo(i5, typeof(Entity), typeof(Entity4)))
                    .ThenInclude(new IncludeExpressionInfo(i4, typeof(Entity), typeof(IEnumerable<Entity5>), typeof(Entity4)))
                    .ThenInclude(new IncludeExpressionInfo(i6, typeof(Entity), typeof(Entity6), typeof(Entity5)));
        }

        return query;
    }

    [Benchmark]
    public object SpecificationsIncludeCached()
    {
        Expression<Func<Entity, Entity2>> i1 = x => x.Prop1;
        Expression<Func<Entity2, Entity3>> i2 = x => x.Prop1;
        Expression<Func<Entity3, Entity4>> i3 = x => x.Prop1;
        Expression<Func<Entity4, IEnumerable<Entity5>>> i4 = x => x.Prop1;
        Expression<Func<Entity, Entity4>> i5 = x => x.Prop2;
        Expression<Func<Entity5, Entity6>> i6 = x => x.Prop1;

        var query = entities
            .IncludeCached(new IncludeExpressionInfo(i1, typeof(Entity), typeof(Entity2)))
                .ThenIncludeCached(new IncludeExpressionInfo(i2, typeof(Entity), typeof(Entity3), typeof(Entity2)))
                .ThenIncludeCached(new IncludeExpressionInfo(i3, typeof(Entity), typeof(Entity4), typeof(Entity3)))
                .ThenIncludeCached(new IncludeExpressionInfo(i4, typeof(Entity), typeof(IEnumerable<Entity5>), typeof(Entity4)))
                .ThenIncludeCached(new IncludeExpressionInfo(i6, typeof(Entity), typeof(Entity6), typeof(IEnumerable<Entity5>)))
            .IncludeCached(new IncludeExpressionInfo(i5, typeof(Entity), typeof(Entity4)))
                .ThenIncludeCached(new IncludeExpressionInfo(i4, typeof(Entity), typeof(IEnumerable<Entity5>), typeof(Entity4)))
                .ThenIncludeCached(new IncludeExpressionInfo(i6, typeof(Entity), typeof(Entity6), typeof(IEnumerable<Entity5>)));
        for (var i = 0; i < RepeatCount; ++i)
        {
            query = entities
                .IncludeCached(new IncludeExpressionInfo(i1, typeof(Entity), typeof(Entity2)))
                    .ThenIncludeCached(new IncludeExpressionInfo(i2, typeof(Entity), typeof(Entity3), typeof(Entity2)))
                    .ThenIncludeCached(new IncludeExpressionInfo(i3, typeof(Entity), typeof(Entity4), typeof(Entity3)))
                    .ThenIncludeCached(new IncludeExpressionInfo(i4, typeof(Entity), typeof(IEnumerable<Entity5>), typeof(Entity4)))
                    .ThenIncludeCached(new IncludeExpressionInfo(i6, typeof(Entity), typeof(Entity6), typeof(IEnumerable<Entity5>)))
                .IncludeCached(new IncludeExpressionInfo(i5, typeof(Entity), typeof(Entity4)))
                    .ThenIncludeCached(new IncludeExpressionInfo(i4, typeof(Entity), typeof(IEnumerable<Entity5>), typeof(Entity4)))
                    .ThenIncludeCached(new IncludeExpressionInfo(i6, typeof(Entity), typeof(Entity6), typeof(IEnumerable<Entity5>)));
        }

        return query;
    }

    [Benchmark]
    public object SpecificationsSpecIncludeCached()
    {
        var query = entities.WithSpecification(new TestSpecificationCached(), evaluator);
        for (var i = 0; i < RepeatCount; ++i)
        { 
            query = entities.WithSpecification(new TestSpecificationCached(), evaluator);
        }

        return query;
    }

    [Benchmark]
    public object SpecificationsSpecInclude()
    {
        var query = entities.WithSpecification(new TestSpecification(), SpecificationEvaluator.Default);
        for (var i = 0; i < RepeatCount; ++i)
        {
            query = entities.WithSpecification(new TestSpecification(), SpecificationEvaluator.Default);
        }

        return query;
    }
}

public class Entity
{
    public int Id { get; set; }
    public Entity2 Prop1 { get; set; }
    public Entity4 Prop2 { get; set; }
}

public class Entity2
{
    public int Id { get; set; }
    public Entity3 Prop1 { get; }
}

public class Entity3
{
    public int Id { get; set; }
    public Entity4 Prop1 { get; }
}

public class Entity4
{
    public int Id { get; set; }
    public IEnumerable<Entity5> Prop1 { get; }
}

public class Entity5
{
    public int Id { get; set; }
    public Entity6 Prop1 { get; set; }
}

public class Entity6
{
    public int Id { get; set; }
}

public sealed class TestSpecification : Specification<Entity>
{
    public TestSpecification()
    {
        this.Query
            .Include(x => x.Prop1).ThenInclude(x => x.Prop1).ThenInclude(x => x.Prop1).ThenInclude(x => x.Prop1).ThenInclude(x => x.Prop1)
            .Include(x => x.Prop2).ThenInclude(x => x.Prop1).ThenInclude(x => x.Prop1);
    }
}

public sealed class TestSpecificationCached : Specification<Entity>
{
    public TestSpecificationCached()
    {
        this.Query
            .Include(x => x.Prop1).ThenIncludeCached(x => x.Prop1).ThenIncludeCached(x => x.Prop1).ThenIncludeCached(x => x.Prop1).ThenIncludeCached(x => x.Prop1)
            .Include(x => x.Prop2).ThenIncludeCached(x => x.Prop1).ThenIncludeCached(x => x.Prop1);
    }
}

public class Context : DbContext
{
    public Context(DbContextOptions options) : base(options)
    {
    }

    public DbSet<Entity> Entities { get; set; }
}