﻿using System;
using System.Reflection;
using System.Threading;
using asm.Core.Data.Entity.Patterns.Repository;
using DatingApp.Model;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DatingApp.Data.Repositories
{
	public class CountryRepository : RepositoryBase<DataContext, Country>, ICountryRepositoryBase
	{
		private static readonly Lazy<PropertyInfo[]> __keyProperties = new Lazy<PropertyInfo[]>(() => new[] { typeof(Country).GetProperty(nameof(Country.Code))}, LazyThreadSafetyMode.PublicationOnly);

		/// <inheritdoc />
		public CountryRepository([NotNull] DataContext context, [NotNull] IConfiguration configuration, ILogger<CityRepository> logger)
			: base(context, configuration, logger)
		{
		}

		/// <inheritdoc />
		protected override PropertyInfo[] KeyProperties => __keyProperties.Value;
	}
}