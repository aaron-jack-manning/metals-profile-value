module UnitConversions

let inline kilogramToOunce kg = kg * 32.1507466

let metals = [
        ("XAU", "Gold")
        ("XAG", "Silver")
        ("XPT", "Platinum")
        ("XPD", "Palladium")
        ("XCU", "Copper")
        ("XRH", "Rhodium")
        ("RUTH", "Ruthenium")
        ("ALU", "Aluminum")
        ("NI", "Nickel")
        ("ZNC", "Zinc")
        ("TIN", "Tin")
        ("LCO", "Cobalt")
        ("IRD", "Iridium")
        ("LEAD", "Lead")
        ("IRON", "Iron Ore")
        ("URANIUM", "Uranium")
        ("BRASS", "Brass")
        ("BRONZE", "Bronze")
        ("MG", "Magnesium")
        ("OSMIUM", "Osmium")
        ("RHENIUM", "Rhenium")
        ("INDIUM", "Indium")
        ("MO", "Molybdenum")
    ]

let nameToSymbol =
    metals
    |> List.map (fun (a, b) -> (b, a))
    |> Map.ofList
    
let symbolToName =
    metals
    |> Map.ofList