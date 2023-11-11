namespace Elmish.HotReload.Bolero

open System
open Elmish

type IStateAccess<'state> =
    abstract TryGetState:  unit -> 'state option
    abstract SetState: 'state -> unit

module Program =
    type Msg<'msg> =
        | UserMsg of 'msg
        | Stop

    module Internal =
        open System.IO
        open System.Text.Json

        let fromAppData<'model> () =
            let savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "test-state.json")
            
            { new IStateAccess<'model> with
                member self.TryGetState () =
                    if (File.Exists(savePath))
                    then
                        Console.WriteLine("loading state")

                        let json = File.ReadAllText(savePath)
                        Console.WriteLine(json)

                        //let options = JsonSerializerOptions()
                        //let jsonDoc = JsonDocument.Parse(json)
                        //jsonDoc.RootElement.
                        let model : 'model = JsonSerializer.Deserialize(json)
                        Some model
                    else
                        Console.WriteLine("no saved state")
                        None

                member self.SetState state =
                    Console.WriteLine($"saving: {JsonSerializer.Serialize(state)}")
                    let serializedModel = JsonSerializer.Serialize(state)
                    File.WriteAllText(savePath, serializedModel)}

        let fromSessionState<'model> (sessionState: Blazored.SessionStorage.ISyncSessionStorageService ) =
            let key = "nya"
            {new IStateAccess<'model> with
                member self.TryGetState () =
                    if sessionState.ContainKey(key)
                    then 
                        Console.WriteLine("loading state")
                        Console.WriteLine(sessionState.GetItemAsString(key))
                        
                        Some (sessionState.GetItem(key))
                    else 
                        Console.WriteLine("no saved state")
                        
                        None

                member self.SetState state =
                    Console.WriteLine($"saving: {state}")
                    sessionState.SetItem(key, state)}

        let fromLocalStorage<'model> (localstore: Blazored.LocalStorage.ISyncLocalStorageService) =
            let key = "nya"
            {new IStateAccess<'model> with
                member self.TryGetState () =
                    if localstore.ContainKey(key)
                    then 
                        Console.WriteLine("loading state")
                        Console.WriteLine(localstore.GetItemAsString(key))
                        
                        Some (localstore.GetItem(key))
                    else 
                        Console.WriteLine("no saved state")
                        
                        None

                member self.SetState state =
                    Console.WriteLine($"saving: {state}")
                    localstore.SetItem(key, state)}


    /// Start the dispatch loop with `'arg` for the init() function.
    let inline withHotReload (sessionStorage: IStateAccess<'model>) (program: Program<'arg, 'model, 'msg, 'view>): Program<'arg, 'model, 'msg, 'view> =
#if !DEBUG
        program
#else
        let mutable hmrState : 'model option = None

        // TODO: need to load state and maybe other assembly load stuff, though I think assembly load will need to have happened before this
        
        hmrState <- sessionStorage.TryGetState ()

        let mapUpdate userUpdate (msg : 'msg) (model : 'model) =
            let newModel,cmd = userUpdate msg model

            hmrState <- Some newModel
            hmrState |> Option.iter sessionStorage.SetState 

            newModel, cmd

        let createModel (model, cmd) =
            model, cmd

        let mapInit userInit args =
            match hmrState with
            | None ->
                let (userModel, userCmd) = userInit args
                userModel, userCmd
            | Some stateValue ->
                stateValue, Cmd.none

        let mapSetState userSetState (userModel : 'model) dispatch =
            userSetState userModel (dispatch)

        let hmrSubscription =
            let handler dispatch =
                hmrState |> Option.iter sessionStorage.SetState 
                //dispatch Stop     
                        
                // nothing to cleanup
                {new System.IDisposable with member _.Dispose () = ()}

            [["Hmr"], handler ]

        let mapSubscribe subscribe model =
            //subscribe
            Sub.batch [
                subscribe model 
                hmrSubscription
            ]

        let mapView userView model dispatch =
            userView model dispatch
            //userView model (UserMsg >> dispatch)

        let mapTermination (predicate, terminate) =
            (predicate, terminate)
            //let mapPredicate =
            //    function
            //    | UserMsg msg -> predicate msg
            //    | Stop -> true

            //mapPredicate, terminate

        program
        |> Program.map mapInit mapUpdate mapView mapSetState mapSubscribe mapTermination
#endif