module Program

open Microsoft.Azure.Functions.Worker
open Microsoft.Extensions.Hosting
open System

open System.Threading.Tasks;
open Microsoft.Extensions.Configuration;
open Microsoft.Extensions.Hosting;
open Microsoft.Azure.Functions.Worker.Configuration;

[<EntryPoint>]
let main args =
    let o =
        Action<HostBuilderContext, IFunctionsWorkerApplicationBuilder>(fun x y ->
            y.UseFunctionExecutionMiddleware() |> ignore
            //failwith "BOOOM"
            ())
    
    let z =
        Action<WorkerOptions>(fun wo ->
            //failwith "BOOOM"
            ())
    
    let host =
        HostBuilder().ConfigureFunctionsWorkerDefaults().Build()
    
    host.Run()
    
    0