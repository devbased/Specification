﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ardalis.Specification.EntityFrameworkCore
{
    public class IncludeEvaluator : IEvaluator
    {
        private IncludeEvaluator() { }
        public static IncludeEvaluator Instance { get; } = new IncludeEvaluator();

        public bool IsCriteriaEvaluator { get; } = false;

        public IQueryable<T> GetQuery<T>(IQueryable<T> query, ISpecification<T> specification) where T : class
        {
            foreach (var includeString in specification.IncludeStrings)
            {
                query = query.Include(includeString);
            }

            foreach (var includeInfo in specification.IncludeExpressions)
            {
                if (includeInfo.Type == IncludeTypeEnum.Include)
                {
                    query = query.Include(includeInfo);
                }
                else if (includeInfo.Type == IncludeTypeEnum.ThenInclude)
                {
                    query = query.ThenInclude(includeInfo);
                }
            }

            return query;
        }
    }

    public class IncludeEvaluatorCached : IEvaluator
    {
        private IncludeEvaluatorCached() { }
        public static IncludeEvaluatorCached Instance { get; } = new IncludeEvaluatorCached();

        public bool IsCriteriaEvaluator { get; } = false;

        public IQueryable<T> GetQuery<T>(IQueryable<T> query, ISpecification<T> specification) where T : class
        {
            foreach (var includeString in specification.IncludeStrings)
            {
                query = query.Include(includeString);
            }

            foreach (var includeInfo in specification.IncludeExpressions)
            {
                if (includeInfo.Type == IncludeTypeEnum.Include)
                {
                    query = query.IncludeCached(includeInfo);
                }
                else if (includeInfo.Type == IncludeTypeEnum.ThenInclude)
                {
                    query = query.ThenIncludeCached(includeInfo);
                }
            }

            return query;
        }
    }
}
