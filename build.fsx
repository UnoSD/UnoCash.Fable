#r "paket: groupref netcorebuild //"
#load ".fake/build.fsx/intellisense.fsx"

#nowarn "52"

open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.JavaScript

// cd UnoCash.Pulumi
// pulumi up

// Update local.settings.json with new storage account

Target.create "PublishApi" (fun _ ->
    !! "UnoCash.Api/**/publish"
    |> Shell.deleteDirs

    let publishOptions (options : DotNet.PublishOptions) =
        { options with Configuration = DotNet.BuildConfiguration.Release }
    
    DotNet.publish publishOptions "UnoCash.Api/UnoCash.Api.csproj"

    let publishDirSeq = !! "UnoCash.Api/**/publish/**/**"
    let publishDir = !! "UnoCash.Api/**/publish" |> Seq.exactlyOne

    Zip.zip publishDir "UnoCash.Api/bin/UnoCash.Api.zip" publishDirSeq
)

Target.create "Clean" (fun _ ->
    !! "src/bin"
    ++ "src/obj"
    ++ "output"
    |> Seq.iter Shell.cleanDir
)

Target.create "Install" (fun _ ->
    DotNet.restore
        (DotNet.Options.withWorkingDirectory "UnoCash.Fulma")
        "UnoCash.Fulma.sln"
)

Target.create "YarnInstall" (fun _ ->
    let yarnParams (yparams : Yarn.YarnParams) =
        { yparams with WorkingDirectory = "UnoCash.Fulma" }

    Yarn.install yarnParams
)

Target.create "PublishFable" (fun _ ->
    Yarn.exec "webpack --mode production"
              (fun o -> { o with WorkingDirectory = "UnoCash.Fulma" })
)

Target.create "WatchFable" (fun _ ->
    Yarn.exec "webpack-dev-server --mode development"
              (fun o -> { o with WorkingDirectory = "UnoCash.Fulma" })
)

Target.create "PulumiPreview" (fun _ ->
    CreateProcess.fromRawCommandLine "pulumi" "preview" |>
    CreateProcess.withWorkingDirectory "UnoCash.Pulumi" |>
    Proc.run |>
    ignore
)

"Clean"
    ==> "Install"
    ==> "YarnInstall"
    ==> "PublishFable"

"WatchFable"
    <== [ "YarnInstall" ]

Target.runOrDefault "PublishFable"