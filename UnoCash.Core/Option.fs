[<AutoOpen>]
module Option

let ofTryTuple =
    function
    | true, value -> Some value
    | false, _    -> None
    
let (|Value|_|) =
    ofTryTuple