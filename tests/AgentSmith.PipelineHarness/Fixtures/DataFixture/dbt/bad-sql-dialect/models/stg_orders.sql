selectt
    order_id,
    customer_id
    amount_cents
from {{ source('raw', 'orders') }}
where
