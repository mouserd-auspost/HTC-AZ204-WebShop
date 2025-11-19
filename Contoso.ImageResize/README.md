## ImageResize Auzure Function App

This is a simple Azure Function App that will receive notifications when an image is added to an Azure "images" blob storage container, where it will then create a thumbnail (100x100) version of the image and save it to a "thumbnails" blob storage container.

### Initial creation of the Azure Function App

The Azure Function App first needs to be created. This can be done by running the following command:

```sh
az functionapp create \
    --name Team03ImageResize \
    --storage-account team03storage \
    --resource-group Team03 \
    --consumption-plan-location australiaeast \
    --runtime dotnet \
    --functions-version 4
```

### Publishing the Azure Function App

One the Azure Function App has been created you need to publish the code that will handle the blob container events. To do this run the following from the project root:

```sh
func azure functionapp publish Team03ImageResize
```

### Pre-requisites

- [Azure Functions Core Tools](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=macos%2Cisolated-process%2Cnode-v4%2Cpython-v2%2Chttp-trigger%2Ccontainer-apps&pivots=programming-language-csharp#v4)

