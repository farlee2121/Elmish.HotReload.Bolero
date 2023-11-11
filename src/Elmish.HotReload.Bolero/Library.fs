module Elmish.HotReload.Bolero.Core

open Blazor.Extensions.Logging
open BlazorSignalR
open Elmish
//open Elmish.HotReload
open Elmish.HotReload.Core
//open Elmish.HotReload.Types
open Microsoft.AspNetCore.SignalR.Client
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.FSharp.Quotations
open System


let getBrowserConsoleLoggerProvider jsRuntime =
    let assembly = typeof<BrowserConsoleLoggerExtensions>.Assembly
    let providerType = assembly.DefinedTypes |> Seq.find (fun t -> t.Name = "BrowserConsoleLoggerProvider")
    let constructor = providerType.GetConstructors().[0]
    constructor.Invoke([| jsRuntime |]) :?> ILoggerProvider


let createConnection jsRuntime navigationManager =
    let builder =
        HubConnectionBuilder()
            .WithUrlBlazor("http://localhost:9876/reloadhub", jsRuntime, navigationManager)
            .ConfigureLogging(fun b ->
                b.AddProvider(getBrowserConsoleLoggerProvider jsRuntime)
                    .AddBrowserConsole() |> ignore
//                    .SetMinimumLevel(LogLevel.Trace) |> ignore
                )

    builder.Services.AddLogging(fun b ->
        b.AddBrowserConsole() |> ignore
        ) |> ignore
    builder.Build()

let connect (log : ILogger) (hub : HubConnection) = async {
        let mutable connected = false
        while not connected do
            try
                do! hub.StartAsync() |> Async.AwaitTask
                connected <- true
            with e ->
                do! Async.Sleep 500
                log.LogInformation <| sprintf "Failed: %A" e.Message
                log.LogTrace (e.ToString())
                printfn "Hot reload reconnecting..."
        printfn "Connected!"
    }

let startConnection (log : ILogger) jsRuntime navigationManager reload =
    log.LogTrace "Attempting to start connection"
    let hub = createConnection jsRuntime navigationManager
    hub.On(methodName = "Update", handler = Action<string, byte[]>(fun fileName file ->
        log.LogDebug <| sprintf "Received file, byte length: %i" file.Length
        try
            updateAssembly fileName file
        with ex ->
            log.LogError(ex, "Failed to update assembly")

        try
            reload()
        with ex ->
            log.LogError(ex,"Failed to reload!")
        )
    ) |> ignore
    connect log hub


module Program =
    type Msg<'msg> =
        | UserMsg of 'msg
        | Stop

    module Internal =
        open System.IO
        open System.Text.Json

        let savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "test-state.json")
        let tryRestoreState (hmrState : 'model ref) =
            // can I instead save the object as bytes and load it as a generic obj before muh
            if (File.Exists(savePath))
            then
                let json = File.ReadAllText(savePath)
                //let options = JsonSerializerOptions()
                //let jsonDoc = JsonDocument.Parse(json)
                //jsonDoc.RootElement.
                let model : 'model = JsonSerializer.Deserialize(json)
                hmrState.Value <- model

        let saveState (hmrState : 'model) =
            let serializedModel = JsonSerializer.Serialize(hmrState)
            File.WriteAllText(savePath, serializedModel) // TODO:


    /// Start the dispatch loop with `'arg` for the init() function.
    let inline withHotReload (program: Program<'arg, 'model, 'msg, 'view>) =
#if !DEBUG
        program
#else
        let hmrState : 'model ref = ref (unbox null)

        // TODO: need to load state and maybe other assembly load stuff, though I think assembly load will need to have happened before this
        
        Internal.tryRestoreState hmrState

        let mapUpdate userUpdate (msg : Msg<'msg>) (model : 'model) =
            let newModel,cmd =
                match msg with
                | UserMsg userMsg ->
                    userUpdate userMsg model

                | Stop ->
                    model, Cmd.none

            hmrState.Value <- newModel

            newModel
            , Cmd.map UserMsg cmd

        let createModel (model, cmd) =
            model, cmd

        let mapInit userInit args =
            if isNull (box hmrState.Value) then
                let (userModel, userCmd) = userInit args

                userModel
                , Cmd.map UserMsg userCmd
            else
                hmrState.Value, Cmd.none

        let mapSetState userSetState (userModel : 'model) dispatch =
            userSetState userModel (UserMsg >> dispatch)

        let hmrSubscription =
            let handler dispatch =
                Internal.saveState hmrState.Value
                dispatch Stop
                        
                // nothing to cleanup
                {new System.IDisposable with member _.Dispose () = ()}

            [["Hmr"], handler ]

        let mapSubscribe subscribe model =
            Sub.batch [
                subscribe model |> Sub.map "HmrUser" UserMsg
                hmrSubscription
            ]

        let mapView userView model dispatch =
            userView model (UserMsg >> dispatch)

        let mapTermination (predicate, terminate) =
            let mapPredicate =
                function
                | UserMsg msg -> predicate msg
                | Stop -> true

            mapPredicate, terminate

        program
        |> Program.map mapInit mapUpdate mapView mapSetState mapSubscribe mapTermination
#endif




    //let withHotReload 
    //    (log:ILogger option) 
    //    (jsRuntime: Microsoft.JSInterop.IJSRuntime) 
    //    (navigationManager: Microsoft.AspNetCore.Components.NavigationManager)
    //    (viewExpr : Expr<'model -> ('msg -> unit) -> 'view>)
    //    (updateExpr : Expr<'msg -> 'model -> 'model * Cmd<'msg>>)
    //    (program : Program<'arg, 'model, 'msg, 'view>)
    //    : Program<'arg, 'model, 'msg, 'view> =


    //    let log =
    //        match log with
    //        | Some l -> l
    //        | None -> (new LoggerFactory()).CreateLogger() :> ILogger

    //    let updater = ProgramUpdater(log, Program.init program, Program.update program, Program.view program)

    //    let viewResolverInfo = Resolve.resolveView viewExpr
    //    let updateResolverInfo = Resolve.resolveUpdate updateExpr

    //    let reload () = reloadPipeline log updater viewResolverInfo updateResolverInfo

    //    (startConnection log jsRuntime navigationManager reload) |> Async.Start



    //    let erasedProg : Program<'arg, obj, obj, 'view> =
    //        let mapInit _ = updater.Init
    //        let mapUpdate _ = updater.Update
    //        let mapView _ = updater.View
    //        let mapSetState _ = (fun model -> updater.View model >> ignore)
    //        let mapSubscribe (f: ('model -> Sub<'msg>)): (obj -> Sub<obj>) =
    //            unbox<'model> 
    //            >> f
    //            >> Sub.map "" box
    //        let mapTermination (typedTermination: ('msg -> bool)*('model -> unit)) : (obj -> bool)*(obj ->unit)= 
    //            let terminateMsg,terminateModel = typedTermination
    //            (unbox<'msg> >> terminateMsg), (unbox<'model> >> terminateModel)
            
    //        program
    //        |> Program.map 
    //            mapInit
    //            mapUpdate
    //            mapView
    //            mapSetState
    //            mapSubscribe
    //            mapTermination


    //    erasedProg
