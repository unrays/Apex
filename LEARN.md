# LEARN.md

## Iteration 1  
Very simple ECS using a Dictionary to associate entities with their components. Easy to write and reason about.

## Iteration 2  
More elegant and generic design, but the added abstractions make it much slower in practice.

## Iteration 3  
Same general approach as iteration 1, but with small structural changes that significantly improve performance.

## Iteration 4  
Component storage is reworked to reduce overhead and simplify access patterns.

## Iteration 5  
Improves how entities and components are looked up, reducing unnecessary work.

## Iteration 6  
Further simplification of the core logic with a focus on faster component access.

## Iteration 7  
Data is packed more tightly in memory to improve iteration speed.

## Iteration 8  
Moves toward more direct collections (arrays/lists) to reduce indirection.

## Iteration 9  
Iteration logic is tightened to avoid touching irrelevant entities.

## Iteration 10  
Focuses on processing large amounts of data in tight loops for better throughput.

## Iteration 11  
Final pass combining previous optimizations and small tweaks for maximum performance.
