using System;
using System.Linq;
using AutoMapper;
using Game.Domain;
using Microsoft.AspNetCore.Mvc;
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

        public UsersController(IUserRepository userRepository, IMapper mapper)
        {
            this.userRepository = userRepository;
            this.mapper = mapper;
        }

        [HttpGet("{userId}", Name = nameof(GetUserById))]
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

        public IActionResult UpdateUser ([FromRoute] Guid userId, [FromBody] MyUserUpdate user)
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
    }
}