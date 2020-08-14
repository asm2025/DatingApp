﻿using System;
using System.Collections.Generic;
using System.Linq;
using MatchNBuy.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using asm.Core.Web.Controllers;
using asm.Patterns.Pagination;
using MatchNBuy.Data.Repositories;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;

namespace MatchNBuy.API.Controllers
{
	[AllowAnonymous]
	[Route("[controller]")]
	public class WeatherController : ApiController
	{
		/*
		 * This is a fake repo. i.e. not making queries against real data but rather generated
		 * So, anything *Async, ToListAsync, CountAsync, etc... will not work because the
		 * IQueryable<Forecast> is just a list.
		 */
		private readonly IForecastRepository _repository;

		public WeatherController([NotNull] IForecastRepository repository, [NotNull] IConfiguration configuration, ILogger<WeatherController> logger)
			: base(configuration, logger)
		{
			_repository = repository;
		}

		[HttpGet]
		public IActionResult List([FromQuery] Pagination pagination)
		{
			pagination ??= new Pagination();
			IQueryable<Forecast> queryable = _repository.List(pagination);
			pagination.Count = int.MaxValue;
			IList<Forecast> forecasts = queryable.ToList(); // Special case sense this is a fake repo: await queryable.Paginate(pagination).ToListAsync(token);
			return Ok(new Paginated<Forecast>(forecasts, pagination));
		}

		[HttpGet("{date}")]
		public IActionResult Get(DateTime date)
		{
			Forecast result = _repository.Get(date);
			return Ok(result);
		}
	}
}