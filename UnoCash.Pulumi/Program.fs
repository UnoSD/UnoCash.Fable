module Program

open Pulumi.FSharp.Azure.ApiManagement.Inputs
open Pulumi.FSharp.NamingConventions.Azure
open Pulumi.FSharp.Azure.ApiManagement
open Microsoft.AspNetCore.StaticFiles
open Pulumi.AzureNative.Authorization
open Pulumi.FSharp.AzureAD.Inputs
open Pulumi.FSharp.Azure.Storage
open Pulumi.AzureNative.Storage
open Pulumi.AzureNative.Web
open Pulumi.FSharp.AzureAD
open Pulumi.FSharp.Outputs
open Pulumi.FSharp.Output
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

let infra () =
    let stackOutputs =
        StackReference(Deployment.Instance.StackName).Outputs
        
    let group =
        Pulumi.FSharp.AzureNative.Resources.resourceGroup {
            name $"rg-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
        }
    
    // It seems to work also without CORS policy, check it and fix it or remove this crap if not needed
    let origins =
        output {
            let! outputs =
                stackOutputs
                
            return match outputs.TryGetValue apiManagementEndpointOutputName with
                   | true, endpoint -> $"http://localhost:8080, {endpoint}"
                   | _              -> "http://localhost:8080"
        }
    
    let storage =
        Pulumi.FSharp.AzureNative.Storage.storageAccount {
            name                   $"sa{workloadShortName}{Deployment.Instance.StackName}{Region.shortName}001"
            resourceGroup          group.Name
            kind                   Kind.StorageV2
            
            Pulumi.FSharp.AzureNative.Storage.Inputs.sku {
                name SkuName.Standard_LRS
            }
        }
        
    Pulumi.FSharp.AzureNative.Storage.blobServiceProperties {
        name             $"bsp-{workloadShortName}-{Deployment.Instance.StackName}-{Region.shortName}-001"
        blobServicesName "default"
        accountName      storage.Name
        resourceGroup    group.Name
        
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
    
    Pulumi.FSharp.AzureNative.Storage.blobContainer {
        name          "receipts"
        accountName   storage.Name
        containerName "receipts"
        resourceGroup group.Name
    }
    
    Pulumi.FSharp.AzureNative.Storage.table {
        name          "expense"
        tableName     "Expense"
        accountName   storage.Name
        resourceGroup group.Name
    }
        
    let webContainer =
        Pulumi.FSharp.AzureNative.Storage.blobContainer {
            name          "web"
            accountName   storage.Name
            containerName "$web"
            resourceGroup group.Name
        }
            
    let buildContainer =
        Pulumi.FSharp.AzureNative.Storage.blobContainer {
            name               "build"
            accountName storage.Name
            resourceGroup group.Name
        }
    
    let functionPlan =
        Pulumi.FSharp.AzureNative.Web.appServicePlan {
            name          $"asp-{workloadShortName}"
            resourceGroup group.Name
            kind          "FunctionApp"
            
            Pulumi.FSharp.AzureNative.Web.Inputs.skuDescription {
                name "Y1"
                //size "Y1"
                tier "Dynamic"
            }
        }

    let apiBlob =
        Pulumi.FSharp.AzureNative.Storage.blob {
            name          $"{workloadShortName}api"
            accountName   storage.Name
            containerName buildContainer.Name
            resourceGroup group.Name
            source        { ArchivePath = config["ApiBuild"] }.ToPulumiType
            
            BlobType.Block
        }

    // let storage = storage {}; let container = storage.container {}; let blob = container.blob {}; container.blob {}
    // ComputationalExpressions on instances

    let appInsights =
        Pulumi.FSharp.AzureNative.Insights.``component`` {
            name            $"appi-{workloadShortName}"
            resourceGroup   group.Name
            applicationType Pulumi.AzureNative.Insights.ApplicationType.Web
            kind            "web"
            retentionInDays 90
        }
        
    let apiManagement =
        service {
            name           $"apim-{workloadShortName}"
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
    
    let swApiPolicyXml =
        output {
            let! url = apiManagement.GatewayUrl
            
            return String.Format(File.ReadAllText("StaticWebsiteApimApiPolicy.xml"), url)
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
            name                    $"{workloadShortName}spaaadapp"
            displayName             $"{workloadShortName}spaaadapp"
            
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
        ListStorageAccountKeys.Invoke(
            ListStorageAccountKeysInvokeArgs(
                AccountName = storage.Name,
                ResourceGroupName = group.Name
                )).Apply(fun x -> x.Keys[0].Value)
    
    let connectionString =
        Output.Format($"DefaultEndpointsProtocol=https;AccountName={storage.Name};AccountKey={accountKey}")
    
    let app =
        Pulumi.FSharp.AzureNative.Web.webApp {
            name                    $"{workloadShortName}app" // -func
            kind                    "functionapp,linux"
            resourceGroup           group.Name
            serverFarmId            functionPlan.Id
            
            Pulumi.FSharp.AzureNative.Web.Inputs.siteConfig {
                Pulumi.FSharp.AzureNative.Web.Inputs.corsSettings {
                    allowedOrigins     apiManagement.GatewayUrl
                    supportCredentials true
                }
            }
            
            Pulumi.FSharp.AzureNative.Web.Inputs.managedServiceIdentity {
                ManagedServiceIdentityType.SystemAssigned
            }
        }
    
    output {
        let! result =
            GetClientConfig.InvokeAsync()
            
        let! uuid =
            (randomUuid { name $"raid-apim-to-sa-ucash-{Deployment.Instance.StackName}-{Region.shortName}-001" }).Id
        
        let ``Storage Blob Data Reader`` = "2a2b9908-6ea1-4ae2-8e65-a410df84e7d1"
        
        Pulumi.FSharp.AzureNative.Authorization.roleAssignment {
            name               $"ra-apim-to-sa-ucash-{Deployment.Instance.StackName}-{Region.shortName}-001"
            roleAssignmentName uuid
            scope              storage.Id
            roleDefinitionId   $"/subscriptions/{result.SubscriptionId}/providers/Microsoft.Authorization/roleDefinitions/{``Storage Blob Data Reader``}"
            principalId        (apiManagement.Identity |> apply (fun x -> x.PrincipalId))
            principalType      PrincipalType.ServicePrincipal
        }
        
        return ()
    }
    
    // Usually that's the AppId, but that would mean setting a value in the app after creation so means two runs, let's
    // avoid that with this.
    let faAdAppIdentifier =
        //(randomUuid { name $"ident-{Deployment.Instance.StackName}-{Region.shortName}-001" }).Id
        "apiapp"
    
    // Currently we use two app, one to access the site and one between APIm and Function
    // We could use the same so that the user can access the functions with their own credentials and not
    // using APIm on their behalf. Problem with this approach is that no one will then stop them from hammering
    // the function. the only way would be to restrict IP range outbound of APIM, but that's not possible in consumption
    // so now APIM authenticates to the function as a service and the jwtToken cookie is used by the app to get the right
    // user (user cannot tamper the token and change user because it's validated at APIM level first)
    let faAdApplication =
        application {
            name                    $"{workloadShortName}faaadapp"
            displayName             $"{workloadShortName}faaadapp"
            
            // Web
            //replyUrls [
            //    input "https://unocashapp4e650e1a.azurewebsites.net/.auth/login/aad/callback"
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
		}
        *)
        
        // 	"signInUrl": "https://unocashapp4e650e1a.azurewebsites.net",
        }
    servicePrincipal {
        name "sp"
        applicationId faAdApplication.ApplicationId
        // assugbnebt required no
    // visibile to users yes
    // enabled for users to sign in yes    
    }
    
    let faAdApplicationSecret =
        applicationPassword {
            name "func-aad-secret"
            applicationObjectId faAdApplication.ObjectId
            displayName "func-secret"
        }
    
    Pulumi.FSharp.AzureNative.Web.webAppApplicationSettings {
        name $"{workloadShortName}appsettings"
        resourceName app.Name
        resourceGroup group.Name
        properties [
            "AzureWebJobsStorage__accountName"        , io storage.Name
            "FUNCTIONS_WORKER_RUNTIME"                , input "dotnet" // ?
            "WEBSITE_RUN_FROM_PACKAGE"                , io apiBlob.Url
            "APPINSIGHTS_INSTRUMENTATIONKEY"          , io appInsights.InstrumentationKey // MI
            "StorageAccountConnectionString"          , io connectionString // APP SETT?
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
            (randomUuid { name $"raakstoacr-ucash-{Deployment.Instance.StackName}-{Region.shortName}-001" }).Id
        
        let ``Storage Blob Data Owner`` = "b7e6dc6d-f1e8-4753-8033-0f276bb0955b"
        
        Pulumi.FSharp.AzureNative.Authorization.roleAssignment {
            name               $"ra-func-to-sa-aks-{Deployment.Instance.StackName}-{Region.shortName}-001"
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
            name                 $"{workloadShortName}apimapifunction"
            resourceName         "api"
            path                 "api"
            resourceGroup        group.Name
            apiManagementName    apiManagement.Name
            displayName          "API"
            protocols            [ "https" ]
            serviceUrl           (app.DefaultHostName.Apply (sprintf "https://%s/api"))
            path                 ""
            revision             "1"
            subscriptionRequired false
        }

    Pulumi.FSharp.AzureNative.Web.webAppAuthSettingsV2 {
        name "authsett"
        resourceName app.Name
        resourceGroup group.Name
        
        Pulumi.FSharp.AzureNative.Web.Inputs.globalValidation {
            requireAuthentication true
            UnauthenticatedClientActionV2.Return403
        }
        
        Pulumi.FSharp.AzureNative.Web.Inputs.identityProviders {
            Pulumi.FSharp.AzureNative.Web.Inputs.azureActiveDirectory {
                enabled true
                
                Pulumi.FSharp.AzureNative.Web.Inputs.azureActiveDirectoryRegistration {
                    clientId faAdApplication.ApplicationId
                    openIdIssuer (Output.Format($"https://sts.windows.net/{tenantId}/v2.0"))
                    clientSecretSettingName "MICROSOFT_PROVIDER_AUTHENTICATION_SECRET"
                }
                
                Pulumi.FSharp.AzureNative.Web.Inputs.azureActiveDirectoryValidation {
                    allowedAudiences [
                        Output.Format($"api://{faAdAppIdentifier}")
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

    apiPolicy {
        name              $"{workloadShortName}apimapifunctionpolicy"
        apiName           apiFunction.Name
        apiManagementName apiFunction.ApiManagementName
        resourceGroup     apiFunction.ResourceGroupName
        xmlContent        apiPolicyFromFile
    }
    
    let apiOperation (httpMethod : string) =
        apiOperation {
            name              $"{workloadShortName}apimapifunction{httpMethod.ToLower()}"
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
            name                 $"{workloadShortName}webconfig"
            resourceName         "apibaseurl"
            storageAccountName   storage.Name
            storageContainerName webContainer.Name
            resourceType         "Block"
            source               { Text = url }.ToPulumiType
        }
        
        return ()
    }

    let getContentType fileName =
        match FileExtensionContentTypeProvider().TryGetContentType(fileName) with
        | true, ct -> ct
        | _        -> null

    let fablePublishDir =
        config["FableBuild"] + "/"
        
    // Add support for parent resource (component) to group all blobs together
    Directory.EnumerateFiles(fablePublishDir, "*", SearchOption.AllDirectories) |>
    Seq.iteri(fun index file -> (blob {
        name                 $"{workloadShortName}blob{index}"
        source               { Path = file }.ToPulumiType
        accessTier           "Hot"
        contentType          (getContentType file[fablePublishDir.Length..])
        resourceType         "Block"
        resourceName         file[fablePublishDir.Length..]
        storageAccountName   storage.Name
        storageContainerName webContainer.Name
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