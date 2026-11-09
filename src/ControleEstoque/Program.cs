using ControleEstoque.DAO;
using ControleEstoque.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// Proteção contra CSRF em todos os POSTs (os formulários recebem o token automaticamente).
builder.Services.AddControllersWithViews(opcoes =>
    opcoes.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// Login por cookie. Toda tela exige login, exceto as marcadas com [AllowAnonymous] (login e erro).
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opcoes =>
    {
        opcoes.LoginPath = "/Login";
        opcoes.AccessDeniedPath = "/Login";
        opcoes.Cookie.Name = "ControleEstoque.Login";
        opcoes.Cookie.HttpOnly = true;
        opcoes.Cookie.SameSite = SameSiteMode.Lax;
        opcoes.ExpireTimeSpan = TimeSpan.FromHours(8);
        opcoes.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(opcoes =>
    opcoes.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Caminho do banco: relativo à pasta do projeto, para não depender de onde o "dotnet run" foi chamado.
var conexao = new SqliteConnectionStringBuilder(builder.Configuration.GetConnectionString("DefaultConnection"));
if (!Path.IsPathRooted(conexao.DataSource))
    conexao.DataSource = Path.Combine(builder.Environment.ContentRootPath, conexao.DataSource);

builder.Services.AddSingleton(new ConexaoDAO(conexao.ToString()));
builder.Services.AddScoped<EstoqueDAO>();
builder.Services.AddScoped<UsuarioDAO>();

var app = builder.Build();

app.Services.GetRequiredService<ConexaoDAO>().CriarBancoSeNecessario();

// Se ainda não existe usuário, cria o padrão definido em appsettings.json (seção "Login").
using (var escopo = app.Services.CreateScope())
{
    var usuarioDAO = escopo.ServiceProvider.GetRequiredService<UsuarioDAO>();

    if (usuarioDAO.Contar() == 0)
    {
        var padrao = new Usuario
        {
            NomeUsuario = builder.Configuration["Login:UsuarioPadrao"] ?? "admin",
            CriadoEm = DateTime.Now
        };
        padrao.DefinirSenha(builder.Configuration["Login:SenhaPadrao"] ?? "admin123");
        usuarioDAO.Inserir(padrao);
    }
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Error", "?codigo={0}");

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("padrao", "{controller=Home}/{action=Index}/{id?}");

app.Run();
