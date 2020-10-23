# Grain Contracker

GrainContracker retrieves and stores grain pricing data from the main Australian grain companies, and present the pricing history to the user.

GrainContracker is a Web App built using the F# SAFE Stack, with Azure Functions running a CRON job to scrape internet data.

## Starting the Web Application locally

1. Run `dotnet tool restore` to download paket and other related nuget tools. (You only need to do this once)

2. Run:

```bash
dotnet fake build -t run
```

Then open `http://localhost:8080` in your browser.

To run concurrently server and client tests in watch mode (run in a new terminal):

```bash
dotnet fake build -t runtests
```

Client tests are available under `http://localhost:8081` in your browser and server tests are running in watch mode in console.

## Starting the Azure Functions locally

**Prerequisites**

- You must have [Azure Functions Core Tools (v3)](https://github.com/Azure/azure-functions-core-tools) installed and set up.
- You should use the [Azurite Azure Storage emulator](https://github.com/Azure/Azurite). Personally, I am using the Azurite docker container and it seems to work without complaint (so far)

**Method**

1. Open the `GrainContracker/src/PriceAutomata` folder in VsCode.
2. Hit `F5`. The functions should all build and start without complaint.

Opening the entire `GrainContracker` folder won't let you run the Functions via debugger, you can basically treat the entire PriceAutomata folder as its own project - that just happens to pull in references from `Shared.fsproj,` and you can target with paket, which will restore any installed packages for you.



## Deploying to Azure

Coming Soon!

## Roadmap

- Build Scraper code using Azure Functions, to download the contract PDFs and store their contents.
- Build the User Application to display the captured data.
- Build a User Service to allow logins and storage of user's storage and sales history
- Build a phone app? (Stretch Goal)