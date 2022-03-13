using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sitecore.AspNet.ExperienceEditor;
using Sitecore.AspNet.Tracking;
using Sitecore.AspNet.RenderingEngine.Localization;
using Sitecore.LayoutService.Client.Extensions;
using Sitecore.LayoutService.Client.Newtonsoft.Extensions;
using Sitecore.LayoutService.Client.Request;
using Sitecore.AspNet.RenderingEngine.Extensions;

namespace Pumpkin
{
    public class Startup
    {
        private SitecoreOptions SitecoreConfiguration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            SitecoreConfiguration = configuration.GetSection(SitecoreOptions.Key).Get<SitecoreOptions>();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Services to support Sitecore's custom routing
            services.AddRouting()
            // You must enable ASP.NET Core localization to utilize localized Sitecore content.
            .AddLocalization(options => options.ResourcesPath = "Resources")
            // Basic ASP.NET Core MVC support. The equivalent of .AddControllerWithViews() + .AddRazorPages()
            .AddMvc()
            // At this time the Layout Service Client requires Json.NET due to limitations in System.Text.Json.
            .AddNewtonsoftJson(o => o.SerializerSettings.SetDefaults());

        // Register the Sitecore Layout Service Client, which will be invoked by the Sitecore Rendering Engine.
        services.AddSitecoreLayoutService()
            // Set default parameters for the Layout Service Client from our bound configuration object.
            .WithDefaultRequestOptions(request =>
            {
            request
                .SiteName(SitecoreConfiguration.DefaultSiteName)
                .ApiKey(SitecoreConfiguration.ApiKey);
            })
            .AddHttpHandler("default", SitecoreConfiguration.LayoutServiceUri)
            .AsDefaultHandler();

        // Register the Sitecore Rendering Engine services.
        services.AddSitecoreRenderingEngine(options =>
        {
            // Register your components here. This can also potentially be handled with custom extension methods for grouping or reflection.
            options
            .AddPartialView("Component")
            //.AddModelBoundView<ComponentViewModel>("Component")
            .AddViewComponent("ViewComponent")
            .AddDefaultPartialView("_ComponentNotFound");
        })
        // Includes forwarding of Scheme as X-Forwarded-Proto to the Layout Service, so that
        // Sitecore Media and other links have the correct scheme.
        .ForwardHeaders()
        // Enable forwarding of relevant headers and client IP for Sitecore Tracking and Personalization.
        .WithTracking()
        // Enable support for the Experience Editor.
        .WithExperienceEditor(options =>
        {
            // Experience Editor integration needs to know the external URL of your rendering host,
            // if behind HTTPS termination or another proxy (like Traefik).
            if (SitecoreConfiguration.RenderingHostUri != null)
            {
            options.ApplicationUrl = SitecoreConfiguration.RenderingHostUri;
            }
        });

        // Enable support for robot detection.
        services.AddSitecoreVisitorIdentification(options =>
        {
            // Usually the SitecoreInstanceUri is same host as the Layout Service, but it can be any Sitecore CD/CM
            // instance which shares same AspNet session with Layout Service. This address should be accessible
            // from the Rendering Host and will be used to proxy robot detection scripts.
            options.SitecoreInstanceUri = SitecoreConfiguration.InstanceUri;
        });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

              // For production content delivery environments, this should not be enabled.
            if (SitecoreConfiguration.EnableExperienceEditor)
            {
                // Enable the Sitecore Experience Editor POST endpoint.
                app.UseSitecoreExperienceEditor();
            }

            app.UseSitecoreVisitorIdentification();

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                // Set default route to error because if a Sitecore route isn't resolved with data, it should display an error page.
                endpoints.MapControllerRoute(
                "error",
                "error",
                new { controller = "Default", action = "Error" }
                );

                endpoints.MapSitecoreLocalizedRoute("sitecore", "Index", "Default");

                endpoints.MapFallbackToController("Index", "Default");
            });
        }
    }
}
