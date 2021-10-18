using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using AutoMapper;
using Game.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Newtonsoft.Json;
using WebApi.Models;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : Controller
    {
        // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация\
        private readonly IUserRepository userRepository;
        private readonly IMapper mapper;
        private LinkGenerator linkGenerator;

        public UsersController(IUserRepository userRepository, IMapper mapper, LinkGenerator linkGenerator)
        {
            this.userRepository = userRepository;
            this.mapper = mapper;
            this.linkGenerator = linkGenerator;
        }

        [HttpGet("{userId}", Name = nameof(GetUserById))]
        [HttpHead("{userId}")]
        [Produces("application/json", "application/xml")]
        public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
        {
            var user = userRepository.FindById(userId);
            if (user is null)
                return NotFound();

            var res = mapper.Map<UserEntity, UserDto>(user);

            return Ok(res);
        }

        [HttpPost]
        [Produces("application/json", "application/xml")]
        public IActionResult CreateUser([FromBody] MyUserDTO user)
        {
            if (user is null)
                return BadRequest();
            
            if ( string.IsNullOrEmpty(user.Login) || !user.Login.All(symbol => char.IsLetterOrDigit(symbol)))
                ModelState.AddModelError(nameof(user.Login), "Некорректный логин");

            if (!ModelState.IsValid)
                return UnprocessableEntity(ModelState);
            
            var userEntity = mapper.Map<MyUserDTO, UserEntity>(user);
            var createdUserEntity = userRepository.Insert(userEntity);

            return CreatedAtRoute(
                nameof(GetUserById),
                new {userId = createdUserEntity.Id},
                createdUserEntity.Id);
            
        }
        
        [HttpPut("{userId}")]
        [Produces("application/json", "application/xml")]
        public IActionResult UpdateUser ([FromRoute] Guid userId, [FromBody] MyUserUpdateDTO user)
        {
            if (user is null || userId == Guid.Empty)
                return BadRequest();
            
            if (!ModelState.IsValid)
                return UnprocessableEntity(ModelState);

            var userUpdate = new UserEntity(userId);
            mapper.Map(user, userUpdate);

            bool isInsert;
            userRepository.UpdateOrInsert(userUpdate, out isInsert);

            if (!isInsert)
                return NoContent();
            
            return CreatedAtRoute(
                nameof(GetUserById),
                new { userId = userUpdate.Id },
                userUpdate.Id);
        }
        [HttpPatch("{userId}")]
        [Produces("application/json", "application/xml")]
        public IActionResult PartiallyUpdateUser([FromRoute] Guid userId, [FromBody] JsonPatchDocument<MyUserUpdateDTO> patchDoc)
        {
            if (patchDoc is null)
                return BadRequest();

            var user = userRepository.FindById(userId);

            if (user is null)
                return NotFound();

            var updateDto = new MyUserUpdateDTO();
            patchDoc.ApplyTo(updateDto, ModelState);
            
            // Валидация по атрибутам
            var isValid = TryValidateModel(updateDto);
            // Другие валидации...

            if (!isValid)
                return UnprocessableEntity(ModelState);

            mapper.Map(updateDto, user);
            userRepository.Update(user);

            return NoContent();
        }
        
        [HttpDelete("{userId}")]

        [Produces("application/json", "application/xml")]
        public IActionResult DeleteUser([FromRoute] Guid userId)
        {
            var user = userRepository.FindById(userId);
            if (user is null)
                return NotFound();
            
            userRepository.Delete(userId);

            return NoContent();
        }
        
        // [HttpGet]
        // [HttpGet("{pageNumber}, {pageSize}")]
        [Produces("application/json", "application/xml")]
        [HttpGet(Name = nameof(GetUsers))]
        public ActionResult<UserDto>  GetUsers()
        {
            var pageNumber = 1;
            var pageSize = 10;

            if (Request.QueryString.HasValue)
            {
                var querySrting = Request.QueryString.Value;
                var queryDict = QueryHelpers.ParseQuery(querySrting);

                if (queryDict.ContainsKey("pageNumber"))
                    pageNumber = int.Parse(queryDict["pageNumber"]);
                if (queryDict.ContainsKey("pageSize"))
                    pageSize = int.Parse(queryDict["pageSize"]);
            }

            if (pageNumber < 1)
                return BadRequest();
            if (pageSize < 1 || pageSize > 20)
                return BadRequest();

            var pageList = userRepository.GetPage(pageNumber, pageSize);
            var users = mapper.Map<IEnumerable<UserDto>>(pageList);
            
            var pPL = linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers), 
                new {pageNumber = pageNumber - 1, pageSize = pageSize});
            var nPL = linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers), 
                new {pageNumber = pageNumber + 1, pageSize = pageSize});

            if (pageNumber == 1)
                pPL = null;
            
            var paginationHeader = new
            {
                previousPageLink = pPL,
                nextPageLink = nPL,
                totalCount = users.Count(),
                pageSize = pageSize,
                currentPage = pageNumber,
                totalPages = users.Count() / pageSize
            };
            Response.Headers.Add("X-Pagination", JsonConvert.SerializeObject(paginationHeader));
            
            return Ok(users);
        }
    }
}