var builder = WebApplication.CreateBuilder(args);
const string BNAFIDSPOLICYNAME = "bnafidspolicy";
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: BNAFIDSPOLICYNAME,
    policy =>
    {
        var prodSiteUrl = builder.Configuration.GetValue<string>("ProductionSiteUrl");
        var devSiteUrl = builder.Configuration.GetValue<string>("DevSiteUrl");

        if (!string.IsNullOrWhiteSpace(prodSiteUrl))
        {
            policy.WithOrigins(prodSiteUrl).WithMethods("GET");
        }
        if (!string.IsNullOrWhiteSpace(devSiteUrl))
        {
            policy.WithOrigins(devSiteUrl).WithMethods("GET");
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
} 
else
{
    app.UseHttpsRedirection();
}

app.UseCors(BNAFIDSPOLICYNAME);


app.UseAuthorization();

app.MapControllers();

app.Run();
