﻿using Application.Interfaces.Commands;
using Application.Interfaces.IMicroservices.Generic;
using Application.Interfaces.IMicroservicesClient;
using Application.Interfaces.Querys;
using Application.Interfaces.Services;
using Application.Services;
using Infraestructure.Commands;
using Infraestructure.MicroservicesClient;
using Infraestructure.Persistence;
using Infraestructure.Querys;
using Infrastructure.MicroservicesClient;
using Infrastructure.MicroservicesClient.GenericClient;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infraestructure
{
    public static class InjectionDependency
    {
        public static void AddInfraestructureLayer(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<LoginDbContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString("ConnectionStringSQLServer"));
            });

            services.AddScoped<IUserServices, UserServices>();
            services.AddScoped<IRolServices, RolServices>();
            services.AddScoped<IUserQuery, UserQuery>();
            services.AddScoped<IUserCommand, UserCommand>();
            services.AddScoped<IRolQuery, RolQuery>();
            services.AddScoped<IUserLogCommand, UserLogCommand>();
            services.AddTransient<IHttpContextAccessor, HttpContextAccessor>();
            services.AddTransient<IGetMicroserviceClient, GetMicroserviceClient>();
            services.AddTransient<IPostMicroserviceClient, PostMicroservicClient>();
            services.AddScoped<ICreateEmployeeClient, CreateEmployeeClient>();
            services.AddScoped<IGetEmployeeClient, GetEmployeeClient>();
        }
    }
}
