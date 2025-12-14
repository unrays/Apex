# LEARN Summary

1. **Iteration 1 – Simple ECS**  
Basic, straightforward ECS with Dictionary<Entity, List<Component>>, easy to read and fast enough.  [oai_citation:1‡GitHub](https://github.com/unrays/Apex/blob/main/LEARN.md?plain=1)

2. **Iteration 2 – Elegant but Slow**  
A more generic/abstract API that feels cleaner, but is much slower in practice.  [oai_citation:2‡GitHub](https://github.com/unrays/Apex/blob/main/LEARN.md?plain=1)

3. **Iteration 3 – Performance Boost**  
Tweaks from iteration 1 to make things significantly faster with leaner component handling.  [oai_citation:3‡GitHub](https://github.com/unrays/Apex/blob/main/LEARN.md?plain=1)

4. **Iteration 4 – Optimized Storage**  
Reworks how components are stored to reduce overhead and improve access patterns.

5. **Iteration 5 – Better Lookup**  
Refines entity/component lookup logic to cut down on expensive searches.

6. **Iteration 6 – Lean Manager**  
Further simplification of manager logic with a focus on direct component access efficiency.

7. **Iteration 7 – Packed Data**  
Makes memory layout denser to reduce cache misses and iteration overhead.

8. **Iteration 8 – Direct Collections**  
Switches to direct arrays/collections for component storage to reduce indirection.

9. **Iteration 9 – Fast Queries**  
Improves iteration speed by tightening how entities are filtered and accessed.

10. **Iteration 10 – Batch Processing**  
Processes large batches of component data in tight loops for throughput gains.

11. **Iteration 11 – Final Tuning**  
Combines the best ideas and micro-optimizations for lowest overall cost and fastest access.
