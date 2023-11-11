namespace Project1.Client

open Microsoft.AspNetCore.Components.WebAssembly.Hosting
open Microsoft.Extensions.DependencyInjection
open System
open System.Net.Http
open Blazored.SessionStorage
open Blazored.LocalStorage
open System.Text.Json.Serialization.Custom

module Program =

    [<EntryPoint>]
    let Main args =
        let builder = WebAssemblyHostBuilder.CreateDefault(args)
        builder.RootComponents.Add<Main.MyApp>("#main")
        builder.Services.AddScoped<HttpClient>(fun _ ->
            new HttpClient(BaseAddress = Uri builder.HostEnvironment.BaseAddress)) |> ignore

        //builder.Services.AddBlazoredSessionStorage() |> ignore
        builder.Services.AddBlazoredLocalStorage(fun config -> 
            let options = config.JsonSerializerOptions
            options.Converters.Add(OptionJsonConverter())
            options.Converters.Add(TupleJsonConverter())
            options.Converters.Add(UntaggedUnionJsonConverter(fun t -> t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Choice<_,_>> ))
            options.Converters.Add(TaggedUnionJsonConverter())
        ) |> ignore
        builder.Build().RunAsync() |> ignore
        0
