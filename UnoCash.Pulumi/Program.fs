module Program

open Pulumi.FSharp.AzureNative.ApiManagement.Inputs
open Pulumi.FSharp.AzureNative.Storage.Inputs
open Pulumi.FSharp.AzureNative.ApiManagement
open Pulumi.FSharp.AzureNative.Authorization
open Pulumi.FSharp.NamingConventions.Azure
open Pulumi.FSharp.AzureNative.Web.Inputs
open Pulumi.FSharp.AzureNative.Resources
open Pulumi.FSharp.AzureNative.Insights
open Pulumi.FSharp.AzureNative.Storage
open Pulumi.AzureNative.ApiManagement
open Microsoft.AspNetCore.StaticFiles
open Pulumi.AzureNative.Authorization
open Pulumi.FSharp.AzureNative.Web
open Pulumi.FSharp.AzureAD.Inputs
open Pulumi.AzureNative.Insights
open Pulumi.AzureNative.Storage
open Pulumi.AzureNative.Web
open Pulumi.FSharp.AzureAD
open Pulumi.FSharp.Outputs
open Pulumi.FSharp.Config
open Pulumi.FSharp.Assets
open Pulumi.FSharp.Random
open System.Diagnostics
open System.Threading
open Pulumi.FSharp
open System.IO
open System
open Pulumi

[<Literal>]
let workloadShortName = "ucsh"
let apiManagementEndpointOutputName = "ApiManagementEndpoint"

// Usually that's the AppId, but that would mean setting a value in the app after creation so means two runs,
// let's avoid that with this.
let faAdAppIdentifier = "apiapp"
        
// let storage = storage {}; let container = storage.container {}; let blob = container.blob {}; container.blob {}
// ComputationalExpressions on instances

// Add support for parent resource (component) to group all blobs together

let infra () =
    //let stackOutputs =
    //    StackReference(Deployment.Instance.StackName).Outputs
        
    let group =
        resourceGroup {
            name $"rg-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
        }
    
    // It seems to work also without CORS policy, check it and fix it or remove this crap if not needed
    //let origins =
    //    output {
    //        let! outputs =
    //            stackOutputs
    //            
    //        return match outputs.TryGetValue apiManagementEndpointOutputName with
    //               | true, endpoint -> $"http://localhost:8080, {endpoint}"
    //               | _              -> "http://localhost:8080"
    //    }
    
    let storage =
        storageAccount {
            name                   $"sa{workloadShortName}{Deployment.Instance.StackName}{Region.shortName}001"
            resourceGroup          group.Name
            kind                   Kind.StorageV2
            
            sku {
                name SkuName.Standard_LRS
            }
        }
        
    blobServiceProperties {
        name             $"bsp-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
        blobServicesName "default"
        accountName      storage.Name
        resourceGroup    group.Name
        
        corsRules {
            corsRules [
                corsRule {
                    //allowedOrigins  origins
                    allowedOrigins  "http://localhost:8080"
                    allowedHeaders  "*"
                    allowedMethods  [ "PUT"; "OPTIONS" ]
                    exposedHeaders  "*"
                    maxAgeInSeconds 5
                }
            ]
        }
    }
    
    blobContainer {
        name          $"sac-receipts-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
        accountName   storage.Name
        containerName "receipts"
        resourceGroup group.Name
    }
    
    table {
        name          $"sat-expenses-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
        tableName     "Expense"
        accountName   storage.Name
        resourceGroup group.Name
    }
        
    let webContainer =
        blobContainer {
            name          $"sac-web-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            accountName   storage.Name
            containerName "$web"
            resourceGroup group.Name
        }
            
    let buildContainer =
        blobContainer {
            name          $"sac-build-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            accountName   storage.Name
            resourceGroup group.Name
        }
    
    let functionPlan =
        appServicePlan {
            name          $"asp-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            resourceGroup group.Name
            kind          "FunctionApp"
            
            skuDescription {
                name "Y1"
                tier "Dynamic"
            }
        }

    let apiBlob =
        blob {
            name          $"sab-api-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            accountName   storage.Name
            containerName buildContainer.Name
            resourceGroup group.Name
            source        { ArchivePath = config["ApiBuild"] }.ToPulumiType
            
            BlobType.Block
        }

    let appInsights =
        ``component`` {
            name            $"appi-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            resourceGroup   group.Name
            applicationType ApplicationType.Web
            kind            "web"
            retentionInDays 90
        }
        
    let apiManagement =
        apiManagementService {
            name           $"apim-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            resourceGroup  group.Name
            publisherEmail "info@uno.cash"
            publisherName  "UnoSD"
            
            apiManagementServiceSkuProperties {
                name     SkuType.Consumption
                capacity 0
            }
            
            apiManagementServiceIdentity {
                resourceType ApimIdentityType.SystemAssigned
            }
        }

    logger {
        name          $"apimlogger-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
        loggerId      "insight"
        loggerType    LoggerType.ApplicationInsights
        serviceName   apiManagement.Name
        resourceGroup group.Name
        
        credentials   [
            "instrumentationKey", appInsights.InstrumentationKey
        ]
    }
        
    let webContainerUrl =
        output {
            let! endpoints = storage.PrimaryEndpoints
            let! containerName = webContainer.Name
            
            return endpoints.Blob + containerName
        }

    let swApi =
        api {
            name                 $"apima-sw-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            apiId                "staticwebsite"
            resourceGroup        group.Name
            serviceName          apiManagement.Name
            displayName          "StaticWebsite"
            protocols            [ Protocol.Http; Protocol.Https ]
            serviceUrl           webContainerUrl
            path                 ""
            apiRevision          "1"
            subscriptionRequired false
        }
    
    let swApiPolicyXml =
        output {
            let! url = apiManagement.GatewayUrl
            
            return String.Format(File.ReadAllText("StaticWebsiteApimApiPolicy.xml"), url)
        }
        
    Pulumi.FSharp.Azure.ApiManagement.apiPolicy {
        name              $"apimap-sw-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
        apiManagementName apiManagement.Name
        apiName           swApi.Name
        resourceGroup     group.Name
        xmlContent        swApiPolicyXml
    }

    let spaAdApplication =
        application {
            name                    $"app-spa-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            displayName             $"app-spa-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            
            groupMembershipClaims   "None"
            
            applicationWeb {
                applicationWebImplicitGrant {
                    accessTokenIssuanceEnabled true
                    idTokenIssuanceEnabled     true
                }
                
                redirectUris [
                    io    (Output.Format($"{apiManagement.GatewayUrl}/"))
                    input "http://localhost:8080/"
                ]
            }
            
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
            name          $"apio-sw-get-index-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            resourceGroup group.Name
            serviceName   apiManagement.Name
            apiId         swApi.Name
            method        "GET"
            operationId   "get-index"
            urlTemplate   "/"
            displayName   "GET index"
        }
    
    Pulumi.FSharp.Azure.ApiManagement.apiOperationPolicy {
        name              $"apiop-sw-get-index-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
        operationId       getIndexOperation.Name
        apiManagementName apiManagement.Name
        apiName           swApi.Name
        resourceGroup     group.Name
        xmlContent        (policyFromFile "StaticWebsiteApimGetIndexOperationPolicy.xml")
    }
        
    let getOperation =
        apiOperation {
            name          $"apio-sw-get-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            resourceGroup group.Name
            serviceName   apiManagement.Name
            apiId         swApi.Name
            method        "GET"
            operationId   "get"
            urlTemplate   "/*"     
            displayName   "GET"
        }
    
    Pulumi.FSharp.Azure.ApiManagement.apiOperationPolicy {
        name              $"apiop-sw-get-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
        operationId       getOperation.Name
        apiManagementName apiManagement.Name
        apiName           swApi.Name
        resourceGroup     group.Name
        xmlContent        (policyFromFile "StaticWebsiteApimGetOperationPolicy.xml")
    }
    
    let postOperation =
        apiOperation {
            name          $"apio-sw-post-token-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            resourceGroup group.Name
            serviceName   apiManagement.Name
            apiId         swApi.Name
            method        "POST"
            operationId   "post-aad-token"
            urlTemplate   "/"
            displayName   "POST AAD token"
        }
    
    Pulumi.FSharp.Azure.ApiManagement.apiOperationPolicy {
        name              $"apiop-sw-post-token-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
        operationId       postOperation.Name
        apiManagementName apiManagement.Name
        apiName           swApi.Name
        resourceGroup     group.Name
        xmlContent        (policyFromFile "StaticWebsiteApimPostOperationPolicy.xml")
    }
    
    let accountKey =
        ListStorageAccountKeys.Invoke(
            ListStorageAccountKeysInvokeArgs(
                AccountName = storage.Name,
                ResourceGroupName = group.Name
                )).Apply(fun x -> x.Keys[0].Value)
    
    let app =
        webApp {
            name          $"func-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            kind          "functionapp,linux"
            resourceGroup group.Name
            serverFarmId  functionPlan.Id
            
            siteConfig {
                corsSettings {
                    allowedOrigins     apiManagement.GatewayUrl
                    supportCredentials true
                }
            }
            
            managedServiceIdentity {
                ManagedServiceIdentityType.SystemAssigned
            }
        }
    
    output {
        let! result =
            GetClientConfig.InvokeAsync()
            
        let! uuid =
            (randomUuid { name $"raid-apim-to-sa-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001" }).Id
        
        let ``Storage Blob Data Reader`` = "2a2b9908-6ea1-4ae2-8e65-a410df84e7d1"
        
        roleAssignment {
            name               $"ra-apim-to-sa-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            roleAssignmentName uuid
            scope              storage.Id
            roleDefinitionId   $"/subscriptions/{result.SubscriptionId}/providers/Microsoft.Authorization/roleDefinitions/{``Storage Blob Data Reader``}"
            principalId        (apiManagement.Identity |> apply (fun x -> x.PrincipalId))
            principalType      PrincipalType.ServicePrincipal
        }
        
        return ()
    }
    
    // Currently we use two app, one to access the site and one between APIm and Function
    // We could use the same so that the user can access the functions with their own credentials and not
    // using APIm on their behalf. Problem with this approach is that no one will then stop them from hammering
    // the function. the only way would be to restrict IP range outbound of APIM, but that's not possible in consumption
    // so now APIM authenticates to the function as a service and the jwtToken cookie is used by the app to get the
    // right user (user cannot tamper the token and change user because it's validated at APIM level first)
    let faAdApplication =
        application {
            name                    $"app-api-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            displayName             $"app-api-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            
            //replyUrls [
            //    input "https://<func hostname>/.auth/login/aad/callback"
            //]            
            
            identifierUris [
                Output.Format($"api://{faAdAppIdentifier}")
            ]
            
            (*
	            "requiredResourceAccess": [
		            {
			            "resourceAppId": "00000003-0000-0000-c000-000000000000",
			            "resourceAccess": [
				            {
					            "id": "e1fe6dd8-ba31-4d61-89e7-88639da4683d",
					            "type": "Scope"
				            }
			            ]
		            },
		        "signInUrl": "https://unocashapp4e650e1a.azurewebsites.net",
            *)
        }
        
    servicePrincipal {
        name          $"app-api-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
        applicationId faAdApplication.ApplicationId    
    }
    
    let faAdApplicationSecret =
        applicationPassword {
            name                $"apps-api-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            displayName         $"apps-api-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            applicationObjectId faAdApplication.ObjectId
        }
    
    webAppApplicationSettings {
        name          $"func-sett-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
        resourceName  app.Name
        resourceGroup group.Name
        
        properties [
            "AzureWebJobsStorage__accountName"        , io storage.Name
            "FUNCTIONS_WORKER_RUNTIME"                , input "dotnet"
            "WEBSITE_RUN_FROM_PACKAGE"                , io apiBlob.Url
            "APPINSIGHTS_INSTRUMENTATIONKEY"          , io appInsights.InstrumentationKey // MI
            "StorageAccountConnectionString"          , io (Output.Format($"DefaultEndpointsProtocol=https;AccountName={storage.Name};AccountKey={accountKey}")) // Replace with MI in app
            "FormRecognizerKey"                       , input config["FormRecognizerKey"] // MI?
            "FormRecognizerEndpoint"                  , input config["FormRecognizerEndpoint"]
            "FUNCTIONS_EXTENSION_VERSION"             , input "~4"
            "MICROSOFT_PROVIDER_AUTHENTICATION_SECRET", io faAdApplicationSecret.Value
        ]
    }

    output {
        let! result =
            GetClientConfig.InvokeAsync()
            
        let! uuid =
            (randomUuid { name $"raid-func-to-sa-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001" }).Id
        
        let ``Storage Blob Data Owner`` = "b7e6dc6d-f1e8-4753-8033-0f276bb0955b"
        
        roleAssignment {
            name               $"ra-func-to-sa-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            roleAssignmentName uuid
            scope              storage.Id
            roleDefinitionId   $"/subscriptions/{result.SubscriptionId}/providers/Microsoft.Authorization/roleDefinitions/{``Storage Blob Data Owner``}"
            principalId        (app.Identity |> apply (fun x -> x.PrincipalId))
            principalType      PrincipalType.ServicePrincipal
        }
        
        return ()
    }
    
    let apiFunction =
        api {
            name                 $"apima-func-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            apiId                "api"
            path                 "api"
            resourceGroup        group.Name
            serviceName          apiManagement.Name
            displayName          "API"
            protocols            [ Protocol.Https ]
            serviceUrl           (app.DefaultHostName.Apply (sprintf "https://%s/api"))
            path                 ""
            apiRevision          "1"
            subscriptionRequired false
        }

    webAppAuthSettingsV2 {
        name          $"func-auth-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
        resourceName  app.Name
        resourceGroup group.Name
        
        globalValidation {
            requireAuthentication true
            UnauthenticatedClientActionV2.Return403
        }
        
        identityProviders {
            azureActiveDirectory {
                enabled true
                
                azureActiveDirectoryRegistration {
                    clientId faAdApplication.ApplicationId
                    openIdIssuer (Output.Format($"https://sts.windows.net/{tenantId}/v2.0"))
                    clientSecretSettingName "MICROSOFT_PROVIDER_AUTHENTICATION_SECRET"
                }
                
                azureActiveDirectoryValidation {
                    allowedAudiences [
                        $"api://{faAdAppIdentifier}"
                    ]
                }
            }
        }
    }

    let apiPolicyFromFile =
        output {
            let! appId =
                spaAdApplication.ApplicationId

            let! tenantId =
                tenantId
            
            return String.Format(File.ReadAllText("APIApimApiPolicy.xml"),
                                 tenantId,
                                 appId,
                                 faAdAppIdentifier)
        }

    Pulumi.FSharp.Azure.ApiManagement.apiPolicy {
        name              $"apimap-func-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
        apiName           apiFunction.Name
        apiManagementName apiManagement.Name
        resourceGroup     group.Name
        xmlContent        apiPolicyFromFile
    }
    
    let apiOperation (httpMethod : string) =
        apiOperation {
            name          $"apio-func-{httpMethod.ToLower()}-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            resourceGroup group.Name
            serviceName   apiManagement.Name
            apiId         apiFunction.Name
            method        httpMethod
            operationId   (httpMethod.ToLower())
            urlTemplate   "/*"     
            displayName   httpMethod
        }
    
    [ "GET"; "POST"; "DELETE"; "PUT" ] |>
    List.map apiOperation
    
    output {
        let! url = apiManagement.GatewayUrl
        
        blob {
            name          $"sab-webconfig-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
            blobName      "apibaseurl"
            accountName   storage.Name
            containerName webContainer.Name
            resourceGroup group.Name
            source        { Text = url }.ToPulumiType
            
            BlobType.Block
        }
        
        return ()
    }

    let getContentType fileName =
        match FileExtensionContentTypeProvider().TryGetContentType(fileName) with
        | true, ct -> ct
        | _        -> null

    let fablePublishDir =
        config["FableBuild"] + "/"
        
    Directory.EnumerateFiles(fablePublishDir, "*", SearchOption.AllDirectories) |>
    Seq.iteri(fun index file -> (blob {
        name          $"sab-{index}-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
        source        { Path = file }.ToPulumiType
        contentType   (getContentType file[fablePublishDir.Length..])
        blobName      file[fablePublishDir.Length..]
        accountName   storage.Name
        containerName webContainer.Name
        resourceGroup group.Name
        
        BlobType.Block
        BlobAccessTier.Hot
    } |> ignore))

    //let speech =
    //    Pulumi.FSharp.Azure.Cognitive.account {
    //        name          $"cog-{appPrefix}-{Deployment.Instance.StackName}-{Region.shortName}-001"
    //        resourceGroup rg.Name
    //        kind          "SpeechServices"
    //        sttSku        { name "S0" }
    //    }

    dict [
        //"IsFirstRun",              isFirstRun                      :> obj
        //apiManagementEndpoint,     apiManagement.GatewayUrl        :> obj
        //"LetsEncryptAccountKey",   certificate.AccountKey          :> obj
        //"Certificate",             certificate.Pem                 :> obj
    ]

type bclList<'a> =
    System.Collections.Generic.List<'a>

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
    | Some "1"    -> Log.Warn $"Awaiting debugger to attach to the process: {Process.GetCurrentProcess().Id}"
                     waitForDebugger ()
    | _           -> ()

    Deployment.runAsyncWithOptions (infra >> async.Return) stackOptions