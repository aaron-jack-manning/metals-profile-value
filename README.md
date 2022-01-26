# Metals Portfolio Value

This is a simple program which calculates the value of a precious metal portfolio relative to the purchase price. It uses [this API](https://www.metals-api.com), which is free up to 50 requests per month.

`Metadata.csv` contains the desired currency to work in, and the API Key.

`Portfolio.csv` contains the types of precious metals, their quantity and purchase price. Each row represents a single purchase of a single metal. For example:

```
Gold,1,0,2021-01-08,2460.20
```

Represents 5 ounces and 1 kilogram of gold, purchased on January 8th 2021, for 2460.20 of whichever currency is specified in the metadata file.

If a purchase price is specified, the profit/loss will be calculated using that, otherwise the market value at the purchase date will be used.

Each row in the portfolio file will correspond with a row in the output file.

After running the program, a file called `Analysis.csv` will be created in the root directory of this repository, containing the purchase price, current market value and difference for each entry, and totals at the bottom.