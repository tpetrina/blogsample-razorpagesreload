using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RazorPagesWatcher
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            services.AddRazorPageWatcher();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseMvc();

            app.UseRazorPageWatcher();
        }
    }

    public class RazorPageNotifierHub : Hub
    {
        public Task Reload()
        {
            return Clients.Others.SendAsync("Reload");
        }
    }

    public static class RazorPageWatcherExtensions
    {
        public static IServiceCollection AddRazorPageWatcher(this IServiceCollection services)
        {
            services.AddSignalR();
            return services;
        }

        public static IApplicationBuilder UseRazorPageWatcher(this IApplicationBuilder app)
        {
            return app
                .UseSignalR(route =>
                {
                    route.MapHub<RazorPageNotifierHub>("/razorpagenotifierhub");
                })
                            .UseMiddleware<RazorPageWatcherMiddleware>();
        }
    }

    public class RazorPageWatcherMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHubContext<RazorPageNotifierHub> hubContext;
        private readonly ILogger<RazorPageWatcherMiddleware> logger;
        private FileSystemWatcher watcher;

        public RazorPageWatcherMiddleware(
            RequestDelegate next,
            IHostingEnvironment env,
            IHubContext<RazorPageNotifierHub> hubContext,
            ILogger<RazorPageWatcherMiddleware> logger)
        {
            this.logger = logger;
            _next = next;
            this.hubContext = hubContext;
            var path = Path.Combine(env.ContentRootPath, "Pages");

            logger.LogInformation($"Watching under {path}");

            watcher = new FileSystemWatcher();

            watcher.Path = path;
            watcher.IncludeSubdirectories = true;
            watcher.Filter = "*.cshtml";

            watcher.Changed += OnChanged;

            watcher.EnableRaisingEvents = true;
        }

        private async void OnChanged(object sender, FileSystemEventArgs e)
        {
            logger.LogInformation($"File changed {e.FullPath}");
            await hubContext.Clients.All.SendAsync("Reload");
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Call the next delegate/middleware in the pipeline
            await _next(context);
        }
    }
}
