-- note: Inserts 25m rows. Takes some time. Be patient!
insert into benchmark.t_test_data
    by name
with how_many_rows_square_rooted$ as (
    select 1000 as sqrt_rows#
),
rows_square_rooted$ as (
    select level as xx
    from dual
    connect by level <= (select sqrt_rows# from how_many_rows_square_rooted$)
)
select (A.xx - 1) * X.sqrt_rows# + B.xx as id
from how_many_rows_square_rooted$ X
    cross join rows_square_rooted$ A
    cross join rows_square_rooted$ B
;
