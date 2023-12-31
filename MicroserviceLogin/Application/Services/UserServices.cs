﻿using Application.DTO.Request;
using Application.DTO.Response.Microservices;
using Application.DTOs.Token;
using Application.DTOs.Users;
using Application.Exceptions;
using Application.Interfaces.Commands;
using Application.Interfaces.IMicroservicesClient;
using Application.Interfaces.Querys;
using Application.Interfaces.Services;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Application.Services
{
    public class UserServices : IUserServices
    {
        private readonly IUserCommand _userCommand;
        private readonly IUserQuery _userQuery;
        private readonly IConfiguration _configuration;
        private readonly IRolQuery _rolQuery;
        private readonly IUserLogCommand _userLogCommand;
        private readonly ICreateEmployeeClient _createEmployeeClient;
        private readonly IGetEmployeeClient _getEmployeeClient;

        public UserServices(IUserCommand userCommand, IUserQuery userQuery, IConfiguration configuration, IRolQuery rolQuery, IUserLogCommand logCommand, ICreateEmployeeClient createEmployeeClient, IGetEmployeeClient employeeClient)
        {
            _userCommand = userCommand;
            _userQuery = userQuery;
            _configuration = configuration;
            _rolQuery = rolQuery;
            _userLogCommand = logCommand;
            _createEmployeeClient = createEmployeeClient;
            _getEmployeeClient = employeeClient;
        }

        public async Task RegisterUser(RegisterUser registerUser)
        {
            if(registerUser.SuperiorId != null && registerUser.SuperiorId < 1)
                throw new LoginException("Formato del id del superior incorrecto");

            if(await _userQuery.GetUserByEmail(registerUser.Email) != null)
                throw new LoginException("El mail ya fue registrado.");

            if(!_rolQuery.ExistRol(registerUser.IdRol))
                throw new LoginException("Id rol incorrecto.");

            User user = new User()
            {
                Email = registerUser.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(registerUser.Password),
                IdRol = registerUser.IdRol
            };

            var ok = await _userCommand.RegisterUser(user) > 0;
            if(!ok)
                throw new UnprocesableContentException("No se pudo registrar el usuario");

            var request = new EmployeeRequest()
            {
                Id = user.Id,
                FirsName = registerUser.FirsName,
                LastName = registerUser.LastName,
                DepartmentId = registerUser.DepartmentId,
                PositionId = registerUser.PositionId,
                SuperiorId = registerUser.SuperiorId,
                IsApprover = registerUser.IsApprover
            };
            try
            {
                await _createEmployeeClient.CreateEmployee(request);
            }
            catch(Exception ex)
            {
                await _userCommand.RemoveUser(user);
                throw new UnprocesableContentException(ex.Message);
            }
        }

        public async Task<TokenDto> Login(LoginUser login)
        {
            User user = await _userQuery.GetUserByEmail(login.Email);

            if(user == null)
                throw new LoginException("El usuario no coincide.");

            string hash = user.Password;
            string pass = login.Password;
            bool isCorrect = BCrypt.Net.BCrypt.Verify(pass, hash);

            if(!isCorrect)
                throw new LoginException("Contraseña incorrecta.");

            EmployeeResponse employee;
            try
            {
                employee = await _getEmployeeClient.GetEmployee(user.Id);
                await GenerateLog(user.Id);
            }
            catch(Exception ex)
            {
                throw new UnprocesableContentException(ex.Message);
            }


            return GenerateToken(user, employee);
        }

        public async Task<bool> GenerateLog(int id)
        {
            UserLog userLog = new UserLog()
            {
                Date = DateTime.Now,
                IdUser = id,
            };

            return await _userLogCommand.InsertUserLog(userLog) > 0;
        }

        public TokenDto GenerateToken(User user, EmployeeResponse employee)
        {

            IConfigurationSection jwt = _configuration.GetSection("JWT");

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTime.Now.ToString()),
                new Claim("id" , user.Id.ToString()),
                new Claim("email" , user.Email),
                new Claim("rol" , user.Rol.Description),
                new Claim("dep" , employee.DepartmentId.ToString()),
                new Claim("company" , employee.CompanyId.ToString()),
                new Claim("isApprover" , employee.IsApprover.ToString()),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.GetSection("Key").Value));
            var signIn = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                _configuration["Jwt:Issuer"],
                _configuration["Jwt:Audience"],
                claims,
                expires: DateTime.UtcNow.AddMinutes(60),
                signingCredentials: signIn
            );

            return new TokenDto()
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                Expiration = token.ValidTo
            };
        }

        public async Task<List<GetUser>> GetAllUsers()
        {
            IEnumerable<User> users = await _userQuery.GetAllUsers();

            List<GetUser> result = users.Select(u => new GetUser()
            {
                Id = u.Id,
                Email = u.Email,
                Rol = u.Rol.Description
            }).ToList();

            return result;
        }
    }
}
