module ResultMonad

let (>>=) (a : Result<'a, 'e>) (f : 'a -> Result<'b, 'e>) : Result<'b, 'e> =
    match a with
    | Ok data -> f data
    | Error message -> Error message

let (>=>) (f : 'a -> Result<'b, 'e>) (g : 'b -> Result<'c, 'e>) : ('a -> Result<'c, 'e>) =
    (fun x -> (f x) >>= g)