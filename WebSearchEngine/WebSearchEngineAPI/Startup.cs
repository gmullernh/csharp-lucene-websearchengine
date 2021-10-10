using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using WebSearchEngineAPI.Persistence;
using WebSearchEngineAPI.Repositories;

namespace WebSearchEngineAPI
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
            services.AddRouting(options => {
                options.LowercaseUrls = true;
                options.LowercaseQueryStrings = true;
            });

            services.AddDbContext<WebCrawlerContext>(opt =>
            {
                opt.UseSqlite("Filename=AppData/websearchengine.db", options => 
                    options.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName));
            });

            services.AddTransient<PageDatabaseRepository>();

            services.AddScoped<Services.SearchEngine.LuceneSearchEngineService>();

            services.AddSingleton<Services.Crawler.CrawlerService>();
            services.AddHostedService(scope => scope.GetService<Services.Crawler.CrawlerService>());

            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
