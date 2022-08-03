module Program

open Pulumi.FSharp.Azure.ApiManagement.Inputs
open Pulumi.FSharp.Azure.AppService.Inputs
open Pulumi.FSharp.Azure.ApiManagement
open Microsoft.AspNetCore.StaticFiles
open Pulumi.FSharp.Azure.AppInsights
open Pulumi.FSharp.Azure.AppService
open Pulumi.FSharp.AzureAD.Inputs
open Pulumi.FSharp.Azure.Storage
open System.Collections.Generic
open Pulumi.Azure.AppService
open Pulumi.FSharp.AzureAD
open Pulumi.FSharp.Outputs
open Pulumi.FSharp.Output
open Pulumi.FSharp.Config
open Pulumi.FSharp.Assets
open System.Diagnostics
open System.Threading
open Pulumi.FSharp
open System.IO
open System
open Pulumi
//open Pulumi.FSharp.Naming

type ParsedSasToken =
    | Valid of string * DateTime
    | ExpiredOrInvalid
    | Missing

// Local backend folder to be created by FAKE
// Fake to invoke Pulumi?
// Create empty sample stacks to upload in Git

[<Literal>]
let appPrefix = "unocash"

let infra() =
    let rec debug () =
        if (Option.ofObj (Environment.GetEnvironmentVariable("DEBUG"))
            |> Option.defaultValue "") <> "yes" || Debugger.IsAttached then
            ()
        else
            Log.Warn(Process.GetCurrentProcess().Id.ToString())
            Thread.Sleep(100)
            debug ()            
    debug ()
            
    let group =
        Pulumi.FSharp.AzureNative.Resources.resourceGroup {
            name $"rg-{appPrefix}"
        }
    
    let stackOutputs =
        StackReference(Deployment.Instance.StackName).Outputs
   
    //let speech =
    //    Pulumi.FSharp.Azure.Cognitive.account {
    //        name          $"cog-{appPrefix}-{Deployment.Instance.StackName}-{Region.shortName}-001"
    //        resourceGroup rg.Name
    //        kind          "SpeechServices"
    //        sttSku        { name "S0" }
    //    }
    
    let apiManagementEndpoint =
        "ApiManagementEndpoint"
    
    let origins, isFirstRun =
        output {
            let! outputs =
                stackOutputs
                
            return match outputs.TryGetValue apiManagementEndpoint with
                   | true, endpoint -> $"http://localhost:8080, {endpoint}", false
                   | _              -> "http://localhost:8080", true
        } |>
        Output.toTuple
    
    let storage =
        Pulumi.FSharp.AzureNative.Storage.storageAccount {
            name                   $"sa{appPrefix}"
            resourceGroup          group.Name
            kind                   Pulumi.AzureNative.Storage.Kind.StorageV2
            
            Pulumi.FSharp.AzureNative.Storage.Inputs.sku {
                name Pulumi.AzureNative.Storage.SkuName.Standard_LRS
            }
        }
        
    Pulumi.FSharp.AzureNative.Storage.blobServiceProperties {
        name "blob-cors"
        blobServicesName "default"
        accountName storage.Name
        resourceGroup group.Name
        
        Pulumi.FSharp.AzureNative.Storage.Inputs.corsRules {
            corsRules [
                Pulumi.FSharp.AzureNative.Storage.Inputs.corsRule {
                    allowedOrigins  origins
                    allowedHeaders  "*"
                    allowedMethods  [ "PUT"; "OPTIONS" ]
                    exposedHeaders  "*"
                    maxAgeInSeconds 5
                }
            ]
        }
    }
        
    container {
        name               "receipts"
        storageAccountName storage.Name
        resourceName       "receipts"
    }
    
    table {
        name               "expense"
        resourceName       "Expense"
        storageAccountName storage.Name
    }
        
    let webContainer =
        container {
            name               "web"
            storageAccountName storage.Name
            resourceName       "$web"
        }
            
    let buildContainer =
        container {
            name               "build"
            storageAccountName storage.Name
        }
    
    let functionPlan =
        plan {
            name          $"asp-{appPrefix}"
            resourceGroup group.Name
            kind          "FunctionApp"
            planSku {
                size "Y1"
                tier "Dynamic"
            }
        }

    let apiBlob =
        blob {
            name                 $"{appPrefix}api"
            storageAccountName   storage.Name
            storageContainerName buildContainer.Name
            resourceType         "Block"
            source               { ArchivePath = config["ApiBuild"] }.ToPulumiType
        }
    
    let codeBlobUrl =
        Pulumi.AzureNative.Storage.ListStorageAccountSAS.Invoke(
            Pulumi.AzureNative.Storage.ListStorageAccountSASInvokeArgs(
                    AccountName = storage.Name,
                    ResourceGroupName = group.Name,
                    Permissions = Pulumi.AzureNative.Storage.Permissions.R,
                    Services = Pulumi.AzureNative.Storage.Services.B,
                    ResourceTypes = Pulumi.AzureNative.Storage.SignedResourceTypes.O,
                    SharedAccessExpiryTime = DateTime.Now.AddDays(1.).ToString("u").Replace(' ', 'T')
                )
            ).Apply<string>(fun x -> Output.Format($"{apiBlob.Url}{x.AccountSasToken}"))

    let appInsights =
        insights {
            name            $"appi-{appPrefix}"
            resourceGroup   group.Name
            applicationType "web"
            retentionInDays 90
        }
        
    let apiManagement =
        service {
            name           $"apim-{appPrefix}"
            resourceGroup  group.Name
            publisherEmail "info@uno.cash"
            publisherName  "UnoSD"
            skuName        "Consumption_0"
            
            serviceIdentity {
                resourceType "SystemAssigned"
            }
        }

    logger {
        name              "unocashapimlog"
        apiManagementName apiManagement.Name
        resourceGroup     group.Name
        
        loggerApplicationInsights {
            instrumentationKey appInsights.InstrumentationKey
        }
    }
        
    let webContainerUrl =
        output {
            let! endpoints = storage.PrimaryEndpoints
            let! containerName = webContainer.Name
            
            return endpoints.Blob + containerName
        }

    let swApi =
        api {
            name                 "unocashapimapi"
            resourceName         "staticwebsite"
            resourceGroup        group.Name
            apiManagementName    apiManagement.Name
            displayName          "StaticWebsite"
            protocols            [ "http"; "https" ]
            serviceUrl           webContainerUrl
            path                 ""
            revision             "1"
            subscriptionRequired false
        }
        
    let sasExpirationOutputName = "SasTokenExpiration"
    let sasTokenOutputName = "SasToken"
    
    let webContainerExpiry = DateTime.Now.AddYears(1)
    
    let webContainerToken =
        Pulumi.AzureNative.Storage.ListStorageAccountSAS.Invoke(
            Pulumi.AzureNative.Storage.ListStorageAccountSASInvokeArgs(
                    AccountName = storage.Name,
                    ResourceGroupName = group.Name,
                    Permissions = Pulumi.AzureNative.Storage.Permissions.R,
                    Services = Pulumi.AzureNative.Storage.Services.B,
                    ResourceTypes = Pulumi.AzureNative.Storage.SignedResourceTypes.C,
                    SharedAccessExpiryTime = webContainerExpiry.ToString("u").Replace(' ', 'T')
                )
            ).Apply<string>(fun x -> x.AccountSasToken)
    
    let token =
        secretOutput {
            let! previousOutputs =
                stackOutputs

            let tokenValidity =
                let getTokenIfValid (expirationString : string) =
                    match DateTime.TryParse expirationString with
                    | true, x when x > DateTime.Now -> Valid (
                                                           previousOutputs[sasTokenOutputName] :?> string,
                                                           x
                                                       )
                    | _                             -> ExpiredOrInvalid
                
                match previousOutputs.TryGetValue sasExpirationOutputName with
                | true, (:? string as exp) -> getTokenIfValid exp
                | _                        -> Missing
            
            return!
                match tokenValidity with
                | Missing
                | ExpiredOrInvalid     -> webContainerToken |> (fun x -> x.Apply(fun y -> ("?" + y, webContainerExpiry)))
                | Valid (sasToken, e ) -> output { return sasToken, e }
        }
    
    let swApiPolicyXml =
        output {
            let! tokenValue, _ =
                token
                
            let! url = apiManagement.GatewayUrl
                
            let apiPolicyXml =
                let queryString =
                    tokenValue.Substring(1).Split('&') |>
                    Array.map ((fun pair -> pair.Split('=')) >>
                               (function | [| key; value |] -> (key, value) | _ -> failwith "Invalid query string")) |>
                    Map.ofArray
                
                String.Format(File.ReadAllText("StaticWebsiteApimApiPolicy.xml"), [|
                    yield url :> obj
                    
                    yield! ["sv";"ss";"srt";"se";"sp";"sig"] |> List.map (fun x -> (Map.find x queryString) |> box)
                    //yield! ["sv";"sr";"st";"se";"sp";"spr";"sig"] |> List.map (fun x -> (Map.find x queryString) |> box)
                |])

            return apiPolicyXml
        }
        
    apiPolicy {
        name              "unocashapimapipolicy"
        apiManagementName swApi.ApiManagementName
        apiName           swApi.Name
        resourceGroup     swApi.ResourceGroupName
        xmlContent        swApiPolicyXml
    }

    let spaAdApplication =
        application {
            name                    $"{appPrefix}spaaadapp"
            displayName             $"{appPrefix}spaaadapp"
            oauth2AllowImplicitFlow true
            groupMembershipClaims   "None"
            
            replyUrls [
                io    apiManagement.GatewayUrl
                input "http://localhost:8080"
            ]            
            
            applicationOptionalClaims {
                idTokens [
                    applicationOptionalClaimsIdToken {
                        name                 "upn"
                        additionalProperties "include_externally_authenticated_upn"
                        essential            true
                    }
                ]
            }
        }
    
    let tenantId =
        output {
            let! config =
                AzureAD.GetClientConfig.InvokeAsync()
                
            return config.TenantId
        }
    
    let policyFromFile fileName =
        output {
            let! appId =
                spaAdApplication.ApplicationId
            
            let! tenantId =
                tenantId
            
            let policy =
                String.Format(File.ReadAllText(fileName),
                              tenantId,
                              appId)
            
            return policy
        }
    
    let getIndexOperation =
        apiOperation {
            name              "unocashapimindexoperation"
            resourceGroup     group.Name
            apiManagementName apiManagement.Name
            apiName           swApi.Name
            method            "GET"
            operationId       "get-index"
            urlTemplate       "/"
            displayName       "GET index"
        }
    
    apiOperationPolicy {
        name              "unocashapimindexoperationpolicy"
        operationId       getIndexOperation.OperationId
        apiManagementName getIndexOperation.ApiManagementName
        apiName           getIndexOperation.ApiName
        resourceGroup     getIndexOperation.ResourceGroupName
        xmlContent        (policyFromFile "StaticWebsiteApimGetIndexOperationPolicy.xml")
    }
        
    let getOperation =
        apiOperation {
            name              "unocashapimgetoperation"
            resourceGroup     group.Name
            apiManagementName apiManagement.Name
            apiName           swApi.Name
            method            "GET"
            operationId       "get"
            urlTemplate       "/*"     
            displayName       "GET"
        }
    
    apiOperationPolicy {
        name              "unocashapimgetoperationpolicy"
        operationId       getOperation.OperationId
        apiManagementName getOperation.ApiManagementName
        apiName           getOperation.ApiName
        resourceGroup     getOperation.ResourceGroupName
        xmlContent        (policyFromFile "StaticWebsiteApimGetOperationPolicy.xml")
    }
    
    let postOperation =
        apiOperation {
            name              "unocashapimpostoperation"
            resourceGroup     group.Name
            apiManagementName apiManagement.Name
            apiName           swApi.Name
            method            "POST"
            operationId       "post-aad-token"
            urlTemplate       "/"
            displayName       "POST AAD token"
        }
    
    apiOperationPolicy {
        name              "unocashapimpostoperationpolicy"
        operationId       postOperation.OperationId
        apiManagementName postOperation.ApiManagementName
        apiName           postOperation.ApiName
        resourceGroup     postOperation.ResourceGroupName
        xmlContent        (policyFromFile "StaticWebsiteApimPostOperationPolicy.xml")
    }
    
    let accountKey =
        Pulumi.AzureNative.Storage.ListStorageAccountKeys.Invoke(
            Pulumi.AzureNative.Storage.ListStorageAccountKeysInvokeArgs(
                AccountName = storage.Name,
                ResourceGroupName = group.Name
                )).Apply(fun x -> x.Keys[0].Value)
    
    let connectionString =
        Output.Format($"DefaultEndpointsProtocol=https;AccountName={storage.Name};AccountKey={accountKey}")
    
    let app =
        functionApp {
            name                    $"{appPrefix}app" // -func
            version                 "~3"
            resourceGroup           group.Name
            appServicePlanId        functionPlan.Id
            storageAccountName      storage.Name
            storageAccountAccessKey accountKey
            
            appSettings     [
                "runtime"                        , input "dotnet"
                "WEBSITE_RUN_FROM_PACKAGE"       , io codeBlobUrl
                "APPINSIGHTS_INSTRUMENTATIONKEY" , io appInsights.InstrumentationKey
                "StorageAccountConnectionString" , io connectionString
                "FormRecognizerKey"              , input config["FormRecognizerKey"]
                "FormRecognizerEndpoint"         , input config["FormRecognizerEndpoint"]
            ]
            
            functionAppSiteConfig {
                functionAppSiteConfigCors {
                    allowedOrigins     apiManagement.GatewayUrl
                    supportCredentials true
                }
            }
        }
    
    let apiFunction =
        api {
            name                 $"{appPrefix}apimapifunction"
            resourceName         "api"
            path                 "api"
            resourceGroup        group.Name
            apiManagementName    apiManagement.Name
            displayName          "API"
            protocols            [ "https" ]
            serviceUrl           (app.DefaultHostname.Apply (sprintf "https://%s/api"))
            path                 ""
            revision             "1"
            subscriptionRequired false
        }
    
    let masterKey =
        output {
            let! functionAppName = app.Name
            let! resourceGroupName = app.ResourceGroupName
            let! _ = app.Id
            
            let! res = GetFunctionAppHostKeys.InvokeAsync(GetFunctionAppHostKeysArgs(Name = functionAppName,
                                                                                     ResourceGroupName = resourceGroupName))
            
            return res.MasterKey
        }    
    
    let nv =
        namedValue {
            name              $"{appPrefix}apimnvfk"
            displayName       "FunctionKey"
            apiManagementName apiManagement.Name
            resourceGroup     apiManagement.ResourceGroupName
            secret            true
            value             masterKey
        }
    
    
    // TODO: Fix this, we should pass the key name instead of creating this fake dependency
    output {
        let! _ = nv.DisplayName
            
        apiPolicy {
            name              $"{appPrefix}apimapifunctionpolicy"
            apiName           apiFunction.Name
            apiManagementName apiFunction.ApiManagementName
            resourceGroup     apiFunction.ResourceGroupName
            xmlContent        (policyFromFile "APIApimApiPolicy.xml")           
        }
        
        return 0
    }

    
    let apiOperation (httpMethod : string) =
        apiOperation {
            name              $"{appPrefix}apimapifunction{httpMethod.ToLower()}"
            resourceGroup     group.Name
            apiManagementName apiManagement.Name
            apiName           apiFunction.Name
            method            httpMethod
            operationId       (httpMethod.ToLower())
            urlTemplate       "/*"     
            displayName       httpMethod
        }
    
    [ "GET"; "POST"; "DELETE"; "PUT" ] |>
    List.map apiOperation
    
    output {
        let! url = apiManagement.GatewayUrl
        
        blob {
            name                 $"{appPrefix}webconfig"
            resourceName         "apibaseurl"
            storageAccountName   storage.Name
            storageContainerName webContainer.Name
            resourceType         "Block"
            source               { Text = url }.ToPulumiType
        }
        
        return 0
    }

    let getContentType fileName =
        match FileExtensionContentTypeProvider().TryGetContentType(fileName) with
        | true, ct -> ct
        | _        -> null

    let fablePublishDir =
        config["FableBuild"] + "/"
    
    Directory.EnumerateFiles(fablePublishDir, "*", SearchOption.AllDirectories) |>
    Seq.iteri(fun index file -> (blob {
        name                 $"{appPrefix}blob{index}"
        source               { Path = file }.ToPulumiType
        accessTier           "Hot"
        contentType          (getContentType file[fablePublishDir.Length..])
        resourceType         "Block"
        resourceName         file[fablePublishDir.Length..]
        storageAccountName   webContainer.StorageAccountName
        storageContainerName webContainer.Name
    } |> ignore))
    
    let sasExpiry =
        output {
            let! _, expiry = token            
            return expiry.ToString("u")
        }
    
    let getExpensesUrl =
        Output.Format($"https://{app.DefaultHostname}/api/GetExpenses?account=Current&code={masterKey}")
    
    dict [
        
        "CODEBLOBURL", codeBlobUrl :> obj
        
        "IsFirstRun",              isFirstRun                      :> obj
        "Hostname",                app.DefaultHostname             :> obj
        "ResourceGroup",           group.Name                      :> obj
        "StorageAccount",          storage.Name                    :> obj
        "StorageConnectionString", connectionString                :> obj
        apiManagementEndpoint,     apiManagement.GatewayUrl        :> obj
        "ApiManagement",           apiManagement.Name              :> obj
        "StaticWebsiteApi",        swApi.Name                      :> obj
        "FunctionApi",             apiFunction.Name                :> obj
        "ApplicationId",           spaAdApplication.ApplicationId  :> obj
        "TenantId",                tenantId                        :> obj
        "FunctionName",            app.Name                        :> obj
        "GetExpensesUrl",          getExpensesUrl                  :> obj
                                                                              
        // Outputs to read on next deployment to check for changes            
        sasTokenOutputName,        token.Apply fst                 :> obj
        sasExpirationOutputName,   sasExpiry                       :> obj
                                                                   
      //"LetsEncryptAccountKey",   certificate.AccountKey          :> obj
      //"Certificate",             certificate.Pem                 :> obj
    ] |> Output.unsecret

type bclList<'a> =
    List<'a>

let ignoreBlobSourceChanges (args : ResourceTransformationArgs) =
    if args.Resource.GetResourceType() = "azure:storage/blob:Blob" then
        args.Options.IgnoreChanges <- bclList(["source"])
    ResourceTransformationResult(args.Args, args.Options) |> Nullable

let stackOptions =
        StackOptions(
            ResourceTransformations =
                bclList([
                    if Environment.GetEnvironmentVariable("AGENT_ID") = null then
                        yield ResourceTransformation(ignoreBlobSourceChanges)
                ]))

[<EntryPoint>]
let main _ =
    let rec waitForDebugger () =
        match Debugger.IsAttached with
        | false -> Thread.Sleep(100)
                   printf "."
                   waitForDebugger ()
        | true  -> printfn " attached"

    match Environment.GetEnvironmentVariable("PULUMI_DEBUG_WAIT") |>
          Option.ofObj |>
          Option.map (fun x -> x.ToLower()) with
    | Some "true"
    | Some "1"    -> printf "Awaiting debugger to attach to the process"
                     waitForDebugger ()
    | _           -> ()

    Deployment.runAsyncWithOptions (infra >> async.Return) stackOptions