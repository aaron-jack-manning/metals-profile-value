open System.Net.Http
open FSharp.Json
open System.IO
open FSharp.Data
open System

open UnitConversions
open ResultMonad

type MetalValues =
    {
        success : bool option;
        timestamp : int64 option;
        historical : bool option;
        date : string option;
        baseCurrency : string option;
        rates : Map<string, double>;
        unit : string option;
    }

type Either<'a, 'b> =
    | Left of 'a
    | Right of 'b

type Metadata =
    {
        baseSymbol : string;
        apiKey : string;
    }

type PortfolioData =
    {
        symbol : string;
        ounces : float;
        purchaseInfo : Either<DateOnly, float>;
    }

type PriceChange =
    {
        metal : string;
        purchasePrice : float;
        currentPrice : float;
        difference : float;
    }

let apiRequest (date : DateOnly option, symbols : string list, metadata : Metadata) =
    
    let baseUrl = "https://www.metals-api.com/api/"
    let endpoint date key symbols baseCurrency =
        baseUrl + date + "?access_key=" + key + "&base=" + baseCurrency + "&symbols=" + (symbols |> List.fold (fun s m -> s + m + ",") "")

    let url =
        match date with
        | Some date ->
            let dateString =
                string date.Year + "-" + string date.Month + "-" + string date.Day
            endpoint dateString metadata.apiKey symbols metadata.baseSymbol
        | None ->
            endpoint "latest" metadata.apiKey symbols metadata.baseSymbol

    let client = new HttpClient ()
    let response =
        client.GetAsync (url)
        |> Async.AwaitTask
        |> Async.RunSynchronously


    let responseBody =
        response.Content.ReadAsStringAsync ()
        |> Async.AwaitTask
        |> Async.RunSynchronously

    printfn "%s" responseBody

    Json.deserialize<MetalValues> (responseBody.Replace ("base", "baseCurrency"))


let readFiles (portfolioPath : string, metadataPath : string) =
    try
        let portfolioFile = CsvFile.Load (portfolioPath, hasHeaders = true)
        let metadataFile = CsvFile.Load (metadataPath, hasHeaders = true)

        Ok (portfolioFile, metadataFile)
    with
        | _ -> Error "An error occured when reading the CSV files. Make sure they are both valid CSVs."

let processMetadataFile (portfolioFile : CsvFile, metadataFile : CsvFile) : Result<CsvFile * Metadata, string> =
    match metadataFile.Headers with
    | Some [|"BaseSymbol"; "APIKey"|] ->
        Ok (portfolioFile, {
            baseSymbol = string ((Seq.item 0 metadataFile.Rows).GetColumn 0);
            apiKey = string ((Seq.item 0 metadataFile.Rows).GetColumn 1);
        })
    | _ -> Error "The provided headers in the Metadata file must be: BaseSymbol,APIKey."

let processPortfolioFile (portfolioFile : CsvFile, metadata : Metadata)  : Result<(string * string * string * string * string) list * Metadata, string> =
    match portfolioFile.Headers with
    | Some [|"Metal"; "Ounces"; "Kilograms"; "PurchaseDate"; "PurchasePrice"|] ->
        Ok (
            portfolioFile.Rows
            |> Seq.map (fun x ->
                (
                    string (x.GetColumn 0),
                    string (x.GetColumn 1),
                    string (x.GetColumn 2),
                    string (x.GetColumn 3),
                    string (x.GetColumn 4)
                ))
            |> List.ofSeq, metadata)
    | _ -> Error "The provided headers in the Metadata file must be: Metal,Ounces,Kilograms,PurchaseDate,PurchasePrice."

let processPortfolioData ((rows : (string * string * string * string * string) list), metadata : Metadata) : Result<PortfolioData list * Metadata, string> =
    let processedRows =
        [
            for row in rows do
                match row with
                | (metal, ounces, kilograms, purchaseDate, purchasePrice) ->
                    match Map.tryFind metal nameToSymbol with
                    | Some symbol ->
                        let ouncesFloat =
                            try float ounces with
                            | _ -> 
                                printfn "%s" $"Warning: An invalid value of \"{ounces}\" was found in the ounces column, and has been zeroed."
                                0.0

                        let kilogramsFloat =
                            try float kilograms with
                            | _ ->
                                printfn "%s" $"Warning: An invalid value of \"{kilograms}\" was found in the kilograms column, and has been zeroed."
                                0.0

                        yield
                            try
                                let purchasePriceFloat = float purchasePrice
                                
                                Ok ({
                                    symbol = symbol;
                                    ounces = ouncesFloat + kilogramToOunce kilogramsFloat
                                    purchaseInfo = Right purchasePriceFloat;
                                })
                            with
                            | _ ->
                                match purchaseDate with
                                | "" ->
                                    Error "Since a valid purchase price was not provided, the date must be provided in its place."
                                | _ ->
                                    let dateList = purchaseDate.Split "-"

                                    if Array.length dateList <> 3 || String.length dateList[0] <> 4 || String.length dateList[1] <> 2 || String.length dateList[2] <> 2 then
                                        Error "As valid purchase price was not provided, the date must be provided in YYYY-MM-DD format."
                                    else
                                        let year = int dateList[0]
                                        let month = int dateList[1]
                                        let day = int dateList[2]

                                        Ok ({
                                            symbol = symbol;
                                            ounces = ouncesFloat + kilogramToOunce kilogramsFloat
                                            purchaseInfo = Left (new DateOnly(year, month, day));
                                        })
                        
                    | None ->
                        Error $"{metal} is an unsupported metal."
        ]
        
    let noErrors =
        processedRows
        |> List.forall (fun x ->
            match x with
            | Ok _ -> true
            | Error _ -> false
        )

    if noErrors then
        Ok (
            processedRows
            |> List.map (fun x ->
                match x with
                | Ok data -> data
                | _ -> failwith "If this error occurs there is an error with the implementation."
            ), metadata
        )
    else
        let error =
            processedRows
            |> List.find (fun x ->
                match x with
                | Error message -> true
                | _ -> false)

        match error with
        | Ok _ ->
            failwith "If this error occurs there is an error with the implementation."
        | Error message -> Error message

let calculateDifference (portfolio : PortfolioData list, metadata : Metadata) =
    
    let uniqueSymbols =
        portfolio
        |> List.map (fun x -> x.symbol)
        |> List.distinct

    let currentPrices = apiRequest (None, uniqueSymbols, metadata)


    let priceChanges =
        [
            for portfolioEntry in portfolio do
                match portfolioEntry.purchaseInfo with
                | Left date ->
                    let historicalPrices = apiRequest (Some date, [portfolioEntry.symbol], metadata)

                    let pricePaid = portfolioEntry.ounces * historicalPrices.rates[portfolioEntry.symbol]

                    (pricePaid, portfolioEntry.symbol, portfolioEntry.ounces)
                | Right pricePaid ->
                    (pricePaid, portfolioEntry.symbol, portfolioEntry.ounces)
        ]
        |> List.map (fun (pricePaid, symbol, ounces) ->
            {
                metal = symbolToName |> Map.find symbol
                purchasePrice = pricePaid
                currentPrice = currentPrices.rates[symbol] * ounces
                difference = currentPrices.rates[symbol] * ounces - pricePaid
            }
        )


    let (totalPaid, totalValue, totalDifference) =
        priceChanges
        |> List.fold (fun (paid, value, difference) m  -> (paid + m.purchasePrice, value + m.currentPrice, difference + m.difference)) (0.0, 0.0, 0.0)

    Ok (priceChanges @ [{
        metal = "Total"
        purchasePrice = totalPaid
        currentPrice = totalValue
        difference = totalDifference
    }])
    
let respond = readFiles >=> processMetadataFile >=> processPortfolioFile >=> processPortfolioData >=> calculateDifference



[<EntryPoint>]
let main argv =

    let fileLocation = @"..\..\..\..\..\"

    let filecontents = 
        match respond (fileLocation + "Portfolio.csv", fileLocation + "Metadata.csv") with
        | Ok prices ->

            let headers = ",PurchasePrice,CurrentValue,Profit\n"

            let rows =
                prices
                |> List.fold (fun s m -> s + m.metal + "," + string m.purchasePrice + "," + string m.currentPrice + "," + string m.difference  + "\n") ""

            headers + rows

        | Error message -> "An error occured: " + message


    File.WriteAllText (fileLocation + "Analysis.csv", filecontents)


    0

