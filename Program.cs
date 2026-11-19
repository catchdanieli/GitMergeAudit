using GitMergeAudit.Services;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<GitAuditService>();
builder.Services.AddSingleton<FolderPickerService>();
var app = builder.Build();
app.UseDefaultFiles(); app.UseStaticFiles();
app.MapPost("/api/folder", async (FolderPickerService service) => {
    try { return Results.Ok(new { path = await service.PickFolder() }); }
    catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
});
app.MapPost("/api/branches", async (RepositoryRequest request, GitAuditService service) => {
    try { return Results.Ok(new { branches = await service.GetBranches(request.Repository) }); }
    catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
});
app.MapPost("/api/audit", async (AuditRequest request, GitAuditService service) => {
    try { return Results.Ok(await service.Analyze(request)); }
    catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
});
app.MapPost("/api/diff", async (DiffRequest request, GitAuditService service) => {
    try { return Results.Ok(await service.Diff(request)); }
    catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
});
app.Run("http://localhost:5078");
public record RepositoryRequest(string Repository);
public record AuditRequest(string Repository, string FirstBranch, string SecondBranch);
public record DiffRequest(string Repository, string FirstBranch, string SecondBranch, string Path);
