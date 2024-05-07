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
        policy.WithOrigins("https://bnafids.netlify.app", "https://bnafids-yz0a--5173--34455753.local-credentialless.webcontainer.io").WithMethods("GET");
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

}

app.UseCors(BNAFIDSPOLICYNAME);

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
