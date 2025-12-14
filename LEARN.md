# LEARN.md

## Iteration 1 – Basic ECS  
A very simple ECS implementation focused on clarity and learning, with no real performance optimizations.

## Iteration 2 – Abstract / Generic  
More generic and conceptually cleaner, but the added abstraction makes it significantly slower.

## Iteration 3 – Component Pools  
Introduces component pooling to reduce allocations and improve reuse.

## Iteration 4 – Type-Based Storage  
Components are stored per type, improving memory locality and iteration patterns.

## Iteration 5 – Dense Entity Storage  
Entities are stored densely in arrays, reducing indirection and cache misses.

## Iteration 6 – Archetypes  
Entities are grouped by component sets (archetypes) to speed up filtering and iteration.

## Iteration 7 – Chunked SoA  
Uses chunked Structure-of-Arrays layout to further optimize cache efficiency.

## Iteration 8 – Direct Access  
Removes most lookups by relying on direct indices for faster component access.

## Iteration 9 – Views / Filters  
Adds views to iterate only over entities matching a system’s required components.

## Iteration 10 – Job-Style Processing  
Processes components in batches, inspired by job systems and data-oriented design.

## Iteration 11 – Cache-Oriented Final  
Most optimized version, focusing on contiguous memory, cache usage, and overall performance.
