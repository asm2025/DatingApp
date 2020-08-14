﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using asm.Core.Web.Controllers;
using asm.Data.Patterns.Parameters;
using asm.Drawing.Helpers;
using asm.Extensions;
using asm.Helpers;
using asm.Patterns.Pagination;
using asm.Patterns.Sorting;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using MatchNBuy.Data.Repositories;
using MatchNBuy.Model;
using MatchNBuy.Model.Parameters;
using MatchNBuy.Model.TransferObjects;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;

namespace MatchNBuy.API.Controllers
{
	[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
	[Route("[controller]")]
	public class UsersController : ApiController
	{
		private readonly IUserRepository _repository;
		private readonly IMapper _mapper;

		/// <inheritdoc />
		public UsersController([NotNull] IUserRepository repository, [NotNull] IMapper mapper, [NotNull] IConfiguration configuration, ILogger<UsersController> logger)
			: base(configuration, logger)
		{
			_repository = repository;
			_mapper = mapper;
		}

		#region User
		[HttpGet]
		public async Task<IActionResult> List([FromQuery] UserList pagination, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			pagination ??= new UserList();

			ListSettings listSettings = _mapper.Map<ListSettings>(pagination);
			listSettings.Include = new[]
			{
				"Photos"
			};

			listSettings.Filter = BuildFilter(User, pagination);

			if (listSettings.OrderBy == null || listSettings.OrderBy.Count == 0)
			{
				listSettings.OrderBy = new[]
				{
					new SortField(nameof(Model.User.LastActive), SortType.Descending),
					new SortField(nameof(Model.User.FirstName)),
					new SortField(nameof(Model.User.LastName))
				};
			}

			IQueryable<User> queryable = _repository.List(listSettings);
			listSettings.Count = await queryable.CountAsync(token);
			token.ThrowIfCancellationRequested();
			
			IList<UserForList> users = await queryable.Paginate(pagination)
													.ProjectTo<UserForList>(_mapper.ConfigurationProvider)
													.ToListAsync(token);
			return Ok(new Paginated<UserForList>(users, listSettings));

			static DynamicFilter BuildFilter(ClaimsPrincipal user, UserList pagination)
			{
				const int AGE_RANGE = 10;

				string userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
				StringBuilder filter = new StringBuilder();
				IList<object> args = new List<object>();
				filter.Append($"{nameof(Model.User.Id)} != @{args.Count}");
				args.Add(userId);
				
				if (pagination.Gender.HasValue && pagination.Gender != Genders.NotSpecified)
				{
					filter.Append($" and {nameof(Model.User.Gender)} == @{args.Count}");
					args.Add((int)pagination.Gender.Value);
				}

				DateTime today = DateTime.Today;
				bool hasMinAge = pagination.MinAge.HasValue && pagination.MinAge > 0;
				bool hasMaxAge = pagination.MaxAge.HasValue && pagination.MaxAge > 0;

				if (!pagination.Likers && !pagination.Likees && (!hasMinAge || !hasMaxAge))
				{
					Claim dobClaim = user.FindFirst(ClaimTypes.DateOfBirth);

					if (dobClaim != null && DateTime.TryParse(dobClaim.Value, out DateTime dob))
					{
						double age = DateTime.Today.Years(dob).NotBelow(Model.User.AGE_MIN);
						if (!hasMinAge) pagination.MinAge = ((int)(age - AGE_RANGE)).NotBelow(Model.User.AGE_MIN);
						if (!hasMaxAge) pagination.MaxAge = (int)(age + AGE_RANGE);
					}
					else
					{
						if (!hasMinAge) pagination.MinAge = Model.User.AGE_MIN;
						if (!hasMaxAge) pagination.MaxAge = Model.User.AGE_MAX;
					}

					if (pagination.MaxAge < pagination.MinAge) pagination.MaxAge = pagination.MinAge.Value + AGE_RANGE;
				}

				if (!pagination.Likers && !pagination.Likees && !pagination.Gender.HasValue)
				{
					Claim genderClaim = user.FindFirst(ClaimTypes.Gender);

					if (genderClaim != null && Enum.TryParse(genderClaim.Value, true, out Genders gender))
					{
						pagination.Gender = gender == Genders.Female
												? Genders.Male
												: Genders.Female;
					}
				}

				if (pagination.MinAge > 0)
				{
					DateTime minDate = today.AddYears(-pagination.MinAge.Value);
					filter.Append($" and {nameof(Model.User.DateOfBirth)} <= DateTime({minDate.Year}, {minDate.Month}, {minDate.Day})");
				}

				if (pagination.MaxAge > 0)
				{
					DateTime maxDate = today.AddYears(-pagination.MaxAge.Value);
					filter.Append($" and {nameof(Model.User.DateOfBirth)} >= DateTime({maxDate.Year}, {maxDate.Month}, {maxDate.Day})");
				}

				if (pagination.Likers)
				{
					filter.Append($" and {nameof(Model.User.Likers)}.Contains(@{args.Count})");
					args.Add(userId);
				}

				if (pagination.Likees)
				{
					filter.Append($" and {nameof(Model.User.Likees)}.Contains(@{args.Count})");
					args.Add(userId);
				}

				return new DynamicFilter
				{
					Expression = filter.ToString(),
					Arguments = args.ToArray()
				};
			}
		}

		[HttpGet("{id}")]
		[SwaggerResponse((int)HttpStatusCode.NotFound)]
		public async Task<IActionResult> Get(string id, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			User user = await _repository.GetAsync(token, id);
			token.ThrowIfCancellationRequested();
			if (user == null) return NotFound(id);
			UserForList userForList = _mapper.Map<UserForList>(user);
			return Ok(userForList);
		}

		[AllowAnonymous]
		[HttpPost("[action]")]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		public async Task<IActionResult> Login([FromBody][NotNull] UserForLogin loginParams, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			User user = await _repository.SignInAsync(loginParams.UserName, loginParams.Password, true, token);
			token.ThrowIfCancellationRequested();
			if (user == null || string.IsNullOrEmpty(user.Token)) return Unauthorized(loginParams.UserName);
			return Ok(new
			{
				token = user.Token,
				user = _mapper.Map<UserForLoginDisplay>(user)
			});
		}

		[AllowAnonymous]
		[HttpPost("[action]")]
		[SwaggerResponse((int)HttpStatusCode.Created)]
		public async Task<IActionResult> Register([FromBody][NotNull] UserToRegister userParams, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			User user = _mapper.Map<User>(userParams);
			user = await _repository.AddAsync(user, userParams.Password, token);
			token.ThrowIfCancellationRequested();
			UserForSerialization userForSerialization = _mapper.Map<UserForSerialization>(user);
			return CreatedAtAction(nameof(Get), new { id = user.Id }, userForSerialization);
		}

		[HttpPut("{id}/[action]")]
		[SwaggerResponse((int)HttpStatusCode.BadRequest)]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		[SwaggerResponse((int)HttpStatusCode.NotFound)]
		public async Task<IActionResult> Update(string id, [FromBody] UserToUpdate userParams, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (string.IsNullOrEmpty(id)) return BadRequest();
			if (!id.IsSame(User.FindFirst(ClaimTypes.NameIdentifier)?.Value) && !User.IsInRole(Role.Administrators)) return Unauthorized(id);
			User user = await _repository.GetAsync(token, id);
			token.ThrowIfCancellationRequested();
			if (user == null) return NotFound(id);
			user = _mapper.Map(userParams, user);
			user.Id = id;
			user = await _repository.UpdateAsync(user, token);
			token.ThrowIfCancellationRequested();
			if (user == null) throw new Exception("Updating user failed.");
			UserForSerialization userForSerialization = _mapper.Map<UserForSerialization>(user);
			return Ok(userForSerialization);
		}

		[HttpDelete("{id}/[action]")]
		[SwaggerResponse((int)HttpStatusCode.BadRequest)]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		[SwaggerResponse((int)HttpStatusCode.NotFound)]
		public async Task<IActionResult> Delete(string id, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (string.IsNullOrEmpty(id)) return BadRequest();
			if (!id.IsSame(User.FindFirst(ClaimTypes.NameIdentifier)?.Value) && !User.IsInRole(Role.Administrators)) return Unauthorized(id);
			User user = await _repository.GetAsync(token, id);
			token.ThrowIfCancellationRequested();
			if (user == null) return NotFound(id);
			await _repository.DeleteAsync(user, token);
			return Ok();
		}
		#endregion

		#region Photos
		[HttpGet("{userId}/[action]")]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		public async Task<IActionResult> Photos(string userId, [FromQuery] SortablePagination pagination, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (string.IsNullOrEmpty(userId) || !userId.IsSame(User.FindFirst(ClaimTypes.NameIdentifier)?.Value) && !User.IsInRole(Role.Administrators)) return Unauthorized(userId);
			
			ListSettings listSettings = _mapper.Map<ListSettings>(pagination);
			StringBuilder filter = new StringBuilder();
			IList<object> args = new List<object>();
			filter.Append($"{nameof(Photo.UserId)} == @{args.Count}");
			args.Add(userId);

			listSettings.Filter = new DynamicFilter
			{
				Expression = filter.ToString(),
				Arguments = args.ToArray()
			};

			IQueryable<Photo> queryable = _repository.Photos.List(listSettings);
			pagination.Count = await queryable.LongCountAsync(token);
			token.ThrowIfCancellationRequested();
			IList<PhotoForList> photos = await queryable.Paginate(pagination)
														.ProjectTo<PhotoForList>(_mapper.ConfigurationProvider)
														.ToListAsync(token);
			token.ThrowIfCancellationRequested();
			return Ok(new Paginated<PhotoForList>(photos, pagination));
		}

		[HttpGet("{userId}/Photos/{id}")]
		[SwaggerResponse((int)HttpStatusCode.BadRequest)]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		[SwaggerResponse((int)HttpStatusCode.NotFound)]
		public async Task<IActionResult> GetPhoto(string userId, Guid id, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (id.IsEmpty()) return BadRequest();
			if (string.IsNullOrEmpty(userId) || !userId.IsSame(User.FindFirst(ClaimTypes.NameIdentifier)?.Value)) return Unauthorized(userId);
			Photo photo = await _repository.Photos.GetAsync(token, id);
			token.ThrowIfCancellationRequested();
			if (photo == null) return NotFound(id);
			if (!userId.IsSame(photo.UserId)) return Unauthorized(userId);
			PhotoForList photoForList = _mapper.Map<PhotoForList>(photo);
			return Ok(photoForList);
		}

		[HttpPost("{userId}/Photos/Add")]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		[SwaggerResponse((int)HttpStatusCode.Created)]
		public async Task<IActionResult> AddPhoto(string userId, [FromForm][NotNull] PhotoToAdd photoParams, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (photoParams.File == null || photoParams.File.Length == 0) throw new InvalidOperationException("No photo was provided to upload.");
			if (string.IsNullOrEmpty(userId) || !userId.IsSame(User.FindFirst(ClaimTypes.NameIdentifier)?.Value)) return Unauthorized(userId);

			Stream stream = null;
			Image image = null;
			Image resizedImage = null;
			string fileName;

			try
			{
				string imagesPath = Path.Combine(Environment.ContentRootPath, _repository.ImageBuilder.BaseUri.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar), userId);
				fileName = Path.Combine(imagesPath, PathHelper.Extenstion(Path.GetFileName(photoParams.File.FileName), _repository.ImageBuilder.FileExtension));
				stream = photoParams.File.OpenReadStream();
				image = Image.FromStream(stream, true, true);
				(int x, int y) = asm.Numeric.Math.AspectRatio(image.Width, image.Height, Configuration.GetValue("images:users:size", 128));
				resizedImage = ImageHelper.Resize(image, x, y);
				fileName = ImageHelper.Save(resizedImage, fileName);
			}
			finally
			{
				ObjectHelper.Dispose(ref stream);
				ObjectHelper.Dispose(ref image);
				ObjectHelper.Dispose(ref resizedImage);
			}

			if (string.IsNullOrEmpty(fileName)) throw new Exception($"Could not upload image for user '{userId}'.");
			fileName = $"{userId}/{Path.GetFileNameWithoutExtension(fileName)}";

			Photo photo = _mapper.Map<Photo>(photoParams);
			photo.UserId = userId;
			photo.Url = _repository.ImageBuilder.Build(fileName).ToString();
			photo = await _repository.Photos.AddAsync(photo, token);
			token.ThrowIfCancellationRequested();
			if (photo == null) throw new Exception($"Add photo '{fileName}' for the user '{userId}' failed.");
			await _repository.Context.SaveChangesAsync(token);
			token.ThrowIfCancellationRequested();
			
			PhotoForList photoForList = _mapper.Map<PhotoForList>(photo);
			return CreatedAtAction(nameof(Get), new { id = photo.Id }, photoForList);
		}

		[HttpPut("{userId}/Photos/{id}/Update")]
		[SwaggerResponse((int)HttpStatusCode.BadRequest)]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		[SwaggerResponse((int)HttpStatusCode.NotFound)]
		public async Task<IActionResult> UpdatePhoto(string userId, Guid id, [FromBody] PhotoToEdit photoToParams, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (id.IsEmpty()) return BadRequest();
			if (string.IsNullOrEmpty(userId) || !userId.IsSame(User.FindFirst(ClaimTypes.NameIdentifier)?.Value)) return Unauthorized(userId);

			Photo photo = await _repository.Photos.GetAsync(token, id);
			token.ThrowIfCancellationRequested();
			if (photo == null) return NotFound(id);
			if (!photo.UserId.IsSame(userId)) return Unauthorized(userId);
			photo = await _repository.Photos.UpdateAsync(_mapper.Map(photoToParams, photo), token);
			token.ThrowIfCancellationRequested();
			if (photo == null) throw new Exception("Updating photo failed.");
			await _repository.Context.SaveChangesAsync(token);
			token.ThrowIfCancellationRequested();

			PhotoForList photoForList = _mapper.Map<PhotoForList>(photo);
			return Ok(photoForList);
		}

		[HttpDelete("{userId}/Photos/{id}/Delete")]
		[SwaggerResponse((int)HttpStatusCode.BadRequest)]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		[SwaggerResponse((int)HttpStatusCode.NotFound)]
		public async Task<IActionResult> DeletePhoto(string userId, Guid id, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (id.IsEmpty()) return BadRequest();
			if (string.IsNullOrEmpty(userId) || !userId.IsSame(User.FindFirst(ClaimTypes.NameIdentifier)?.Value)) return Unauthorized(userId);
			
			Photo photo = await _repository.Photos.GetAsync(token, id);
			token.ThrowIfCancellationRequested();
			if (photo == null) return NotFound(id);
			if (!photo.UserId.IsSame(userId) && !User.IsInRole(Role.Administrators)) return Unauthorized(userId);
			photo = await _repository.Photos.DeleteAsync(photo, token);
			token.ThrowIfCancellationRequested();
			if (photo == null) throw new Exception("Deleting photo failed.");
			await _repository.Context.SaveChangesAsync(token);
			return Ok();
		}

		[HttpGet("{userId}/Photos/Default")]
		[SwaggerResponse((int)HttpStatusCode.BadRequest)]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		[SwaggerResponse((int)HttpStatusCode.NotFound)]
		public async Task<IActionResult> GetDefaultPhoto(string userId, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (string.IsNullOrEmpty(userId) || !userId.IsSame(User.FindFirst(ClaimTypes.NameIdentifier)?.Value)) return Unauthorized(userId);

			Photo photo = await _repository.Photos.GetDefaultAsync(userId, token);
			if (photo == null) return NotFound(userId);
			if (!photo.UserId.IsSame(userId)) return Unauthorized(userId);

			PhotoForList photoForList = _mapper.Map<PhotoForList>(photo);
			return Ok(photoForList);
		}

		[HttpPut("{userId}/Photos/{id}/SetDefault")]
		[SwaggerResponse((int)HttpStatusCode.BadRequest)]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		[SwaggerResponse((int)HttpStatusCode.NotFound)]
		public async Task<IActionResult> SetDefaultPhoto(string userId, Guid id, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (id.IsEmpty()) return BadRequest();
			if (string.IsNullOrEmpty(userId) || !userId.IsSame(User.FindFirst(ClaimTypes.NameIdentifier)?.Value)) return Unauthorized(userId);

			Photo photo = await _repository.Photos.GetAsync(new object[] {id}, token);
			token.ThrowIfCancellationRequested();
			if (photo == null) return NotFound(id);
			if (!photo.UserId.IsSame(userId)) return Unauthorized(userId);
			if (!await _repository.Photos.SetDefaultAsync(photo, token)) return NotFound(id);
			return Ok();
		}
		#endregion

		#region Messages
		[HttpGet("{userId}/[action]")]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		public async Task<IActionResult> Messages(string userId, [FromQuery] SortablePagination pagination, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (string.IsNullOrEmpty(userId) || !userId.IsSame(User.FindFirst(ClaimTypes.NameIdentifier)?.Value)) return Unauthorized(userId);
			pagination ??= new MessageList();
			
			IQueryable<Message> queryable = _repository.Messages.List(userId, pagination);
			pagination.Count = await queryable.CountAsync(token);
			token.ThrowIfCancellationRequested();

			IList<MessageForList> messages = await queryable.Paginate(pagination)
															.ProjectTo<MessageForList>(_mapper.ConfigurationProvider)
															.ToListAsync(token);
			token.ThrowIfCancellationRequested();
			return Ok(new Paginated<MessageForList>(messages, pagination));
		}

		[HttpGet("{userId}/Messages/[action]")]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		public async Task<IActionResult> Threads(string userId, [FromQuery] SortablePagination pagination, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (string.IsNullOrEmpty(userId) || !userId.IsSame(User.FindFirst(ClaimTypes.NameIdentifier)?.Value)) return Unauthorized(userId);
			pagination ??= new MessageList();
			
			Paginated<MessageThread> threads = await _repository.Messages.ThreadsAsync(userId, pagination, token);
			token.ThrowIfCancellationRequested();
			return Ok(threads);
		}

		[HttpGet("{userId}/Messages/[action]/{recipientId}")]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		public async Task<IActionResult> Thread(string userId, string recipientId, [FromQuery] SortablePagination pagination, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (string.IsNullOrEmpty(userId)) return Unauthorized(userId);
			if (string.IsNullOrEmpty(recipientId)) return BadRequest(recipientId);
			
			string claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (!userId.IsSame(claimId) && !recipientId.IsSame(claimId)) return Unauthorized(userId);
			
			IQueryable<Message> queryable = _repository.Messages.Thread(userId, recipientId, pagination);
			pagination.Count = await queryable.CountAsync(token);
			token.ThrowIfCancellationRequested();

			IList<MessageForList> messages = await queryable.Paginate(pagination)
															.ProjectTo<MessageForList>(_mapper.ConfigurationProvider)
															.ToListAsync(token);
			token.ThrowIfCancellationRequested();
			return Ok(new Paginated<MessageForList>(messages, pagination));
		}

		[HttpGet("{userId}/Messages/{id}")]
		[SwaggerResponse((int)HttpStatusCode.BadRequest)]
		[SwaggerResponse((int)HttpStatusCode.NotFound)]
		public async Task<IActionResult> GetMessage(Guid id, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (id.IsEmpty()) return BadRequest();
			
			Message message = await _repository.Messages.GetAsync(token, id);
			token.ThrowIfCancellationRequested();
			if (message == null) return NotFound(id);
			
			MessageForList messageForList = _mapper.Map<MessageForList>(message);
			return Ok(messageForList);
		}

		[HttpPost("{userId}/Messages/Add")]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		[SwaggerResponse((int)HttpStatusCode.Created)]
		public async Task<IActionResult> AddMessage(string userId, [FromBody][NotNull] MessageToAdd messageParams, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (string.IsNullOrEmpty(userId) || !userId.IsSame(User.FindFirst(ClaimTypes.NameIdentifier)?.Value)) return Unauthorized(userId);
			
			Message message = _mapper.Map<Message>(messageParams);
			message.SenderId = userId;
			message = await _repository.Messages.AddAsync(message, token);
			token.ThrowIfCancellationRequested();
			if (message == null) throw new Exception($"Add message for the user '{userId}' failed.");
			await _repository.Context.SaveChangesAsync(token);
			token.ThrowIfCancellationRequested();
			
			MessageForList messageForList = _mapper.Map<MessageForList>(message);
			return CreatedAtAction(nameof(Get), new { id = message.Id }, messageForList);
		}

		[HttpPut("{userId}/Messages/{id}/Update")]
		[SwaggerResponse((int)HttpStatusCode.BadRequest)]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		[SwaggerResponse((int)HttpStatusCode.NotFound)]
		public async Task<IActionResult> UpdateMessage(Guid id, [FromBody] MessageToEdit messageToParams, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (id.IsEmpty()) return BadRequest();

			string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId)) return Unauthorized(userId);

			Message message = await _repository.Messages.GetAsync(token, id);
			token.ThrowIfCancellationRequested();
			if (message == null) return NotFound(id);
			if (!message.SenderId.IsSame(userId)) return Unauthorized(userId);
			message = await _repository.Messages.UpdateAsync(_mapper.Map(messageToParams, message), token);
			token.ThrowIfCancellationRequested();
			if (message == null) throw new Exception("Updating message failed.");
			await _repository.Context.SaveChangesAsync(token);
			token.ThrowIfCancellationRequested();
			
			MessageForList messageForList = _mapper.Map<MessageForList>(message);
			return Ok(messageForList);
		}

		[HttpDelete("{userId}/Messages/{id}/Delete")]
		[SwaggerResponse((int)HttpStatusCode.BadRequest)]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		[SwaggerResponse((int)HttpStatusCode.NotFound)]
		public async Task<IActionResult> DeleteMessage(Guid id, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (id.IsEmpty()) return BadRequest();

			string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId)) return Unauthorized(userId);

			Message message = await _repository.Messages.GetAsync(token, id);
			token.ThrowIfCancellationRequested();
			if (message == null) return NotFound(id);
			if (!message.SenderId.IsSame(userId) && !User.IsInRole(Role.Administrators)) return Unauthorized(userId);
			message = await _repository.Messages.DeleteAsync(message, token);
			token.ThrowIfCancellationRequested();
			if (message == null) throw new Exception("Deleting message failed.");
			await _repository.Context.SaveChangesAsync(token);
			return Ok();
		}
		#endregion

		#region Likes
		[HttpPost("{userId}/[action]/{id}")]
		[SwaggerResponse((int)HttpStatusCode.BadRequest)]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		public async Task<IActionResult> Like(string userId, string recipientId, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (string.IsNullOrEmpty(userId) || !userId.IsSame(User.FindFirst(ClaimTypes.NameIdentifier)?.Value)) return Unauthorized(userId);
			if (string.IsNullOrEmpty(recipientId)) return BadRequest(recipientId);
			if (!await _repository.LikeAsync(userId, recipientId, token)) return BadRequest(recipientId);
			return Ok();
		}

		[HttpPost("{userId}/[action]/{id}")]
		[SwaggerResponse((int)HttpStatusCode.BadRequest)]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		public async Task<IActionResult> Unlike(string userId, string recipientId, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (string.IsNullOrEmpty(userId) || !userId.IsSame(User.FindFirst(ClaimTypes.NameIdentifier)?.Value)) return Unauthorized(userId);
			if (string.IsNullOrEmpty(recipientId)) return BadRequest(recipientId);
			if (!await _repository.UnlikeAsync(userId, recipientId, token)) return BadRequest(recipientId);
			return Ok();
		}

		[HttpPost("{userId}/[action]/{id}")]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		public async Task<IActionResult> Likes(string userId, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (string.IsNullOrEmpty(userId) || !userId.IsSame(User.FindFirst(ClaimTypes.NameIdentifier)?.Value)) return Unauthorized(userId);
			int count = await _repository.LikesAsync(userId, token);
			return Ok(count);
		}

		[HttpPost("{userId}/[action]/{id}")]
		[SwaggerResponse((int)HttpStatusCode.Unauthorized)]
		public async Task<IActionResult> Likees(string userId, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (string.IsNullOrEmpty(userId) || !userId.IsSame(User.FindFirst(ClaimTypes.NameIdentifier)?.Value)) return Unauthorized(userId);
			int count = await _repository.LikeesAsync(userId, token);
			return Ok(count);
		}
		#endregion
	}
}