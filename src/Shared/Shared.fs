namespace Shared

open System
open Domain

type Todo = { Id: Guid; Description: string }

module Todo =
    let isValid (description: string) =
        String.IsNullOrWhiteSpace description |> not

    let create (description: string) =
        { Id = Guid.NewGuid()
          Description = description }

module Route =
    let builder typeName methodName = sprintf "/api/%s/%s" typeName methodName

type IGrainConTrackerApi =
    { getTodos: unit -> Async<Todo list>
      addTodo: Todo -> Async<Todo>
      getDayPrices: unit -> Async<DayPrice list> }
