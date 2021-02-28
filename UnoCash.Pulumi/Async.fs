module Async

let map f async' =
    async.Bind(async', f >> async.Return)

let zip (x : Async<'a>) (y : Async<'b>) : Async<'a * 'b> =
  [| map box x; map box y |] |>
  Async.Parallel |>
  map (fun xy -> unbox xy.[0], unbox xy.[1])
  

type AsyncBuilder with
   member _.MergeSources (x, y) = zip x y