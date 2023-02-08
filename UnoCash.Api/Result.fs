[<AutoOpen>]
module Result

type ResultBuilder() =
    member _.Bind(v, f) = Result.bind f v

    member _.Return v = Ok v

    member this.MergeSources(x, y) =
        this.MergeSources(x, y |> Result.mapError (fun y -> [ y ]))

    member _.MergeSources(x, y) =
        match x, y with
        | Ok x, Ok y -> Ok(x, y)
        | Error x, Error y -> Error(x :: y)
        | _, Error y -> Error y
        | Error x, _ -> Error [ x ]

let result = ResultBuilder()
