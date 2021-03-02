module Program

open Pulumi.FSharp.Azure.ApiManagement.Inputs
open Pulumi.FSharp.Azure.AppService.Inputs
open Pulumi.FSharp.AzureStorageSasToken
open Pulumi.FSharp.Azure.Storage.Inputs
open Pulumi.FSharp.Azure.ApiManagement
open Microsoft.AspNetCore.StaticFiles
open Pulumi.FSharp.Azure.AppInsights
open Pulumi.FSharp.Azure.AppService
open Pulumi.FSharp.AzureAD.Inputs
open Pulumi.FSharp.Azure.Storage
open System.Collections.Generic
open Pulumi.FSharp.Azure.Core
open Pulumi.Azure.AppService
open Pulumi.FSharp.AzureAD
open Pulumi.FSharp.Output
open Pulumi.FSharp.Config
open Pulumi.FSharp.Assets
open System.Diagnostics
open System.Threading
open Pulumi.FSharp
open System.IO
open System
open Pulumi

type ParsedSasToken =
    | Valid of string * DateTime
    | ExpiredOrInvalid
    | Missing

// Local backend folder to be created by FAKE
// Fake to invoke Pulumi?
// Create empty sample stacks to upload in Git

let infra() =
    let group =
        resourceGroup {
            name "unocash"
        }
    
    let storage =
        account {
            name                   "unocashstorage"
            resourceGroup          group.Name
            accountReplicationType "LRS"
            accountTier            "Standard"
            
            accountBlobProperties {
                corsRules [
                    accountBlobPropertiesCorsRule {
                        allowedOrigins  "http://localhost:8080"
                        //allowedOrigins  (apiManagement.GatewayUrl.Apply (fun x -> $"http://localhost:8080, {x}"))
                        allowedHeaders  "*"
                        allowedMethods  [ "PUT"; "OPTIONS" ]
                        exposedHeaders  "*"
                        maxAgeInSeconds 5
                    }
                ]
            }
        }
        
    container {
        name               "unocashreceipts"
        storageAccountName storage.Name
        resourceName       "receipts"
    }
        
    let webContainer =
        container {
            name               "unocashweb"
            storageAccountName storage.Name
            resourceName       "$web"
        }
            
    let buildContainer =
        container {
            name               "unocashbuild"
            storageAccountName storage.Name
        }
    
    let functionPlan =
        plan {
            name          "unocashasp"
            resourceGroup group.Name
            kind          "FunctionApp"
            planSku {
                size "Y1"
                tier "Dynamic"
            }
        }

    let apiBlob =
        blob {
            name                 "unocashapi"
            storageAccountName   storage.Name
            storageContainerName buildContainer.Name
            resourceType         "Block"
            source               { Path = config.["ApiBuild"] }.ToPulumiType
        }
    
    let codeBlobUrl =
        sasToken {
            account storage
            blob    apiBlob
        }

    let appInsights =
        insights {
            name            "unocashai"
            resourceGroup   group.Name
            applicationType "web"
            retentionInDays 90
        }
        
    let apiManagement =
        service {
            name           "unocashapim"
            resourceGroup  group.Name
            publisherEmail "info@uno.cash"
            publisherName  "UnoSD"
            skuName        "Consumption_0"
            
            serviceIdentity {
                resourceType "SystemAssigned"
            }
        }
        
    let stackOutputs =
        StackReference(Deployment.Instance.StackName).Outputs

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
            let! blobEndpoint = storage.PrimaryBlobEndpoint
            let! containerName = webContainer.Name
            
            return blobEndpoint + containerName
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
    
    let token =
        secretOutput {
            let! previousOutputs =
                stackOutputs

            let tokenValidity =
                let getTokenIfValid (expirationString : string) =
                    match DateTime.TryParse expirationString with
                    | true, x when x > DateTime.Now -> Valid (
                                                           previousOutputs.[sasTokenOutputName] :?> string,
                                                           x
                                                       )
                    | _                             -> ExpiredOrInvalid
                
                match previousOutputs.TryGetValue sasExpirationOutputName with
                | true, (:? string as exp) -> getTokenIfValid exp
                | _                        -> Missing
            
            return!
                match tokenValidity with
                | Missing
                | ExpiredOrInvalid      -> let expiry = DateTime.Now.AddYears(1)
                                           sasToken {
                                               account    storage
                                               container  webContainer
                                               duration   {
                                                   From = DateTime.Now
                                                   To   = expiry
                                               }
                                               permission Read
                                           } |> (fun x -> x.Apply(fun y -> (y, expiry)))
                 | Valid (sasToken, e ) -> output { return sasToken, e }
        }
    
    let swApiPolicyXml =
        output {
            let! (tokenValue, _) =
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
                    
                    yield! ["sv";"sr";"st";"se";"sp";"spr";"sig"] |> List.map (fun x -> (Map.find x queryString) |> box)
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
            name                    "unocashspaaadapp"
            displayName             "unocashspaaadapp"
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
    
    let policyFromFile fileName =
        output {
            let! appId =
                spaAdApplication.ApplicationId
            
            let! config =
                AzureAD.GetClientConfig.InvokeAsync()
            
            let policy =
                String.Format(File.ReadAllText(fileName),
                              config.TenantId,
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
    
    let app =
        functionApp {
            name                    "unocashapp"
            version                 "~3"
            resourceGroup           group.Name
            appServicePlanId        functionPlan.Id
            storageAccountName      storage.Name
            storageAccountAccessKey storage.PrimaryAccessKey
            
            appSettings     [
                "runtime"                        , input "dotnet"
                "WEBSITE_RUN_FROM_PACKAGE"       , io codeBlobUrl
                "APPINSIGHTS_INSTRUMENTATIONKEY" , io appInsights.InstrumentationKey
                "StorageAccountConnectionString" , io storage.PrimaryConnectionString
                "FormRecognizerKey"              , input config.["FormRecognizerKey"]
                "FormRecognizerEndpoint"         , input config.["FormRecognizerEndpoint"]
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
            name                 "unocashapimapifunction"
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
            name              "unocashapimnvfk"
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
            name              "unocashapimapifunctionpolicy"
            apiName           apiFunction.Name
            apiManagementName apiFunction.ApiManagementName
            resourceGroup     apiFunction.ResourceGroupName
            xmlContent        (policyFromFile "APIApimApiPolicy.xml")           
        }
        
        return 0
    }

    
    let apiOperation (httpMethod : string) =
        apiOperation {
            name              $"unocashapimapifunction{httpMethod.ToLower()}"
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
            name                 "unocashwebconfig"
            resourceName         "apibaseurl"
            storageAccountName   storage.Name
            storageContainerName webContainer.Name
            resourceType         "Block"
            source               { Text = url }.ToPulumiType
        }
        
        return 0
    }

    let fulmaPublishDir =
        "/home/uno/savs/sourcecode/dotNET/UnoCash/UnoCash.Fulma/output/"

    let getContentType fileName =
        match FileExtensionContentTypeProvider().TryGetContentType(fileName) with
        | true, ct -> ct
        | _        -> null

    Directory.EnumerateFiles(fulmaPublishDir, "*", SearchOption.AllDirectories) |>
    Seq.iteri(fun index file -> (blob {
        name                 $"unocashblob{index}"
        source               { Path = file }.ToPulumiType
        accessTier           "Hot"
        contentType          (getContentType file.[fulmaPublishDir.Length..])
        resourceType         "Block"
        resourceName         file.[fulmaPublishDir.Length..]
        storageAccountName   webContainer.StorageAccountName
        storageContainerName webContainer.Name
    } |> ignore))
    
    let sasExpiry =
        output {
            let! (_, expiry) = token            
            return expiry.ToString("u")
        }
    
    dict [
        "Hostname",                           app.DefaultHostname            :> obj
        "ResourceGroup",                      group.Name                     :> obj
        "StorageAccount",                     storage.Name                   :> obj
        "ApiManagementEndpoint",              apiManagement.GatewayUrl       :> obj
        "ApiManagement",                      apiManagement.Name             :> obj
        "StaticWebsiteApi",                   swApi.Name                     :> obj
        "FunctionApi",                        apiFunction.Name               :> obj
        "ApplicationId",                      spaAdApplication.ApplicationId :> obj
        "FunctionName",                       app.Name                       :> obj
        
        // Outputs to read on next deployment to check for changes
        sasTokenOutputName,                   token.Apply fst                :> obj
        sasExpirationOutputName,              sasExpiry                      :> obj

      //"LetsEncryptAccountKey",              certificate.AccountKey         :> obj
      //"Certificate",                        certificate.Pem                :> obj
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