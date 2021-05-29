module Async

let map f async' =
    async.Bind(async', f >> async.Return)
    
let bind f async' =
    async.Bind(async', f)