[<AutoOpen>]
module Result

type ResultBuilder() =
    member __.Bind(v, f) = Result.bind f v

    member __.Return v = Ok v

    member __.MergeSources(x, y) =
        __.MergeSources(x, y |> Result.mapError (fun y -> [ y ]))

    member __.MergeSources(x, y) =
        match x, y with
        | Ok x, Ok y -> Ok(x, y)
        | Error x, Error y -> Error(x :: y)
        | _, Error y -> Error y
        | Error x, _ -> Error [ x ]

let result = ResultBuilder()
