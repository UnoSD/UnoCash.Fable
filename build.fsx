#r "paket: groupref netcorebuild //"
#load ".fake/build.fsx/intellisense.fsx"

#nowarn "52"

open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.JavaScript
open Newtonsoft.Json.Linq
open Microsoft.Identity.Client
open Fake.Net

// Install pulumi/az cli/yarn

// Requires pulumi manual approval
// dotnet fake run build.fsx -t Deploy

module Pulumi =
    let private pulumi' redirectOutput args = 
        CreateProcess.fromRawCommandLine "pulumi" args |>
        CreateProcess.withWorkingDirectory "UnoCash.Pulumi" |>
        redirectOutput |>
        Proc.run

    let private pulumi args =
        pulumi' id args |> ignore
    
    let setConfig key value =
        $"config set %s{key} %s{value}" |>
        pulumi

    let up () =
        pulumi "up"

    let destroy () =
        pulumi "destroy"

    let preview () =
        pulumi "preview"

    let tryGetStackOutput key isSecret =
        (if isSecret then "--show-secrets" else "") |>
        sprintf "stack output %s %s" key |>
        pulumi' CreateProcess.redirectOutput |>
        (fun o -> match o.ExitCode with
                  | 0 -> Some o.Result.Output
                  | _ -> None) |>
        Option.map (String.trimEndChars [| '\n' |])

Target.create "AzCliSetUp" (fun _ ->
    // Install az cli only if not present
    CreateProcess.fromRawCommandLine "curl" "-L https://aka.ms/InstallAzureCli | bash" |>
    Proc.run |>
    ignore

    // az login if not logged in
    CreateProcess.fromRawCommandLine "az" "login" |>
    Proc.run |>
    ignore

    // az account set --subscription <from config or prompt?>
    // az login if not logged in
    CreateProcess.fromRawCommandLine "az" "account set --subscription <>" |>
    Proc.run |>
    ignore
)

Target.create "PublishApi" (fun _ ->
    !! "UnoCash.Api/**/publish"
    |> Shell.deleteDirs

    let publishOptions (options : DotNet.PublishOptions) =
        { options with Configuration = DotNet.BuildConfiguration.Release }
    
    DotNet.publish publishOptions "UnoCash.Api/UnoCash.Api.fsproj"

    let publishDirSeq = !! "UnoCash.Api/**/publish/**/**"
    let publishDir = !! "UnoCash.Api/**/publish" |> Seq.exactlyOne

    Zip.zip publishDir "UnoCash.Api/bin/UnoCash.Api.zip" publishDirSeq
)

Target.create "Clean" (fun _ ->
    !! "**/bin"
    ++ "**/obj"
    ++ "**/output"
    -- "**/.fable/**"
    -- "**/node_modules/**"
    |> Seq.iter ((fun x -> Trace.log x; x) >> Shell.cleanDir)
)

Target.create "Install" (fun _ ->
    DotNet.restore (fun x -> { x with MSBuildParams = { x.MSBuildParams with DisableInternalBinLog = true } })
                   "UnoCash.sln"
)

Target.create "YarnInstall" (fun _ ->
    let yarnParams (yparams : Yarn.YarnParams) =
        { yparams with WorkingDirectory = "UnoCash.Fable" }

    Yarn.install yarnParams
)

Target.create "PublishFable" (fun _ ->
    // Get MD5 of the project, if unchanged, don't run
    Yarn.exec "webpack --mode production"
              (fun o -> { o with WorkingDirectory = "UnoCash.Fable" })
)

Target.create "WatchFable" (fun _ ->
    Yarn.exec "webpack-dev-server --mode development"
              (fun o -> { o with WorkingDirectory = "UnoCash.Fable" })
)

Target.create "PulumiPreview" (fun _ ->
    Pulumi.preview()
)

Target.create "PulumiSetVariables" (fun _ ->
    !! "UnoCash.Api/**/publish" |>
    Seq.exactlyOne |>
    Pulumi.setConfig "UnoCash.Pulumi:ApiBuild"
    
    !! "UnoCash.Fable/output" |>
    Seq.exactlyOne |>
    Pulumi.setConfig "UnoCash.Pulumi:FableBuild"
)

Target.create "PulumiUp" (fun _ ->
    // Twice to apply the outputs of the first run (figure a way of running the target twice)
    // If not the first time, run only once; also, use --yes once subscription is confirmed
    
    match Pulumi.tryGetStackOutput "IsFirstRun" true |> Option.map bool.Parse with
    | Some false -> Pulumi.up()
    | _          -> Pulumi.up(); Pulumi.up()
)

Target.create "BackupData" (fun _ ->
    let codeRequest (dcr : DeviceCodeResult) =
        printfn $"{dcr.UserCode} https://aka.ms/devicelogin"
        System.Threading.Tasks.Task.CompletedTask
    
    let appId =
        Pulumi.tryGetStackOutput "ApplicationId" false |>
        Option.defaultWith (fun _ -> failwith "Cannot find application ID")

    let tenantId =
        Pulumi.tryGetStackOutput "TenantId" false |>
        Option.defaultWith (fun _ -> failwith "Cannot find tenant ID")
        
    let url =
        Pulumi.tryGetStackOutput "GetExpensesUrl" false |>
        Option.defaultWith (fun _ -> failwith "Cannot find expenses URL")

    let idToken =
        PublicClientApplicationBuilder.Create(appId)
                                      .WithTenantId(tenantId)
                                      .Build()
                                      .AcquireTokenWithDeviceCode([], fun x -> codeRequest x)
                                      .ExecuteAsync()
                                      .Result
                                      .IdToken

    let response =
        Http.getWithHeaders "" "" (fun headers -> headers.Add("Cookie", "jwtToken=" + idToken)) url |>
        snd
    
    File.writeString false "backup.json" response
)

Target.create "PulumiDestroy" (fun _ ->
    Pulumi.destroy()
)

Target.create "UpdateDevelopmentApiLocalSettings" (fun _ ->
    let connectionString =
        Pulumi.tryGetStackOutput "StorageConnectionString" true |>
        Option.defaultWith (fun _ -> failwith "Missing StorageConnectionString in Pulumi outputs") |>
        JToken.op_Implicit

    let settingsFilePath =
        "UnoCash.Api/local.settings.json"

    let json = 
        File.readAsString settingsFilePath |>
        JObject.Parse
   
    json.["Values"].["AzureWebJobsStorage"] <- connectionString
    json.["Values"].["StorageAccountConnectionString"] <- connectionString

    json.ToString() |>
    File.writeString false settingsFilePath
)

Target.create "Publish" ignore
Target.create "Deploy" ignore

"Install"
    ==> "YarnInstall"
    ==> "PublishFable"

"WatchFable"
    <== [ "YarnInstall" ]

"Publish"
    <== [ "PublishFable"; "PublishApi" ]

"Publish"
    ==> "PulumiSetVariables"
    ==> "PulumiUp"
    // Twice to apply the outputs of the first run
    //==> "PulumiUp"
    =?> ("UpdateDevelopmentApiLocalSettings", BuildServer.isLocalBuild)
    ==> "Deploy"

"BackupData"
    ==> "PulumiDestroy"

Target.runOrDefault "PublishFable"