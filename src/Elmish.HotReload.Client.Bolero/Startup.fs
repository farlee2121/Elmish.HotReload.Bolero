namespace HotReload.Client

open Blazor.Extensions.Logging
open Microsoft.AspNetCore.Blazor.Hosting
open Microsoft.AspNetCore.Components.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Components.WebAssembly.Hosting

type Startup() =

    member __.ConfigureServices(services: IServiceCollection) =
        services.AddLogging(fun builder ->
            builder.AddBrowserConsole()
                .SetMinimumLevel(LogLevel.Trace)
            |> ignore
        ) |> ignore


    member __.Configure(app: IComponentsApplicationBuilder) =
       app.AddComponent<Main.MyApp>("#main")



module Program =

    [<EntryPoint>]
    let Main args =
        // let builder = WebAssemblyHostBuilder.CreateDefault(args)
        // builder.RootComponents.Add<Main.MyApp>("#main")
        // //builder.Services.AddBoleroRemoting(builder.HostEnvironment) |> ignore
        // builder.Build().RunAsync() |> ignore
        // 0
        
        BlazorWebAssemblyHost
           .CreateDefaultBuilder()
           .UseBlazorStartup<Startup>()
           .Build()
           .Run()
        0
