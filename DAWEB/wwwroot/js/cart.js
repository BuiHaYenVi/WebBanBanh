function getCart() {
    let cart = localStorage.getItem("cart");
    return cart ? JSON.parse(cart) : [];
}

function saveCart(cart) {
    localStorage.setItem("cart", JSON.stringify(cart));
}

function addToCart(id, name, price) {

    let cart = getCart();

    let item = cart.find(p => p.id === id);

    if (item) {
        item.quantity++;
    } else {
        cart.push({
            id: id,
            name: name,
            price: price,
            quantity: 1
        });
    }

    saveCart(cart);

    alert("Đã thêm vào giỏ hàng");
}
function loadCart() {

    let cart = getCart();

    let html = "";
    let total = 0;

    cart.forEach((p, index) => {

        let price = parsePrice(p.price);
        let sum = price * p.quantity;
        total += sum;

        html += `
        <tr>
        <td>${p.name}</td>
        <td>${p.price}</td>

        <td>
        <button onclick="decrease(${index})">-</button>
        ${p.quantity}
        <button onclick="increase(${index})">+</button>
        </td>

        <td>${sum.toLocaleString("vi-VN")}đ</td>

        <td>
        <button onclick="removeItem(${index})">X</button>
        </td>

        </tr>
        `;
    });

    document.getElementById("cart-items").innerHTML = html;

    document.getElementById("total").innerText =
        total.toLocaleString("vi-VN") + " "; }

window.onload = loadCart;
function increase(index) {

    let cart = getCart();

    cart[index].quantity++;

    saveCart(cart);

    loadCart();
}

function decrease(index) {

    let cart = getCart();

    if (cart[index].quantity > 1) {
        cart[index].quantity--;
    }

    saveCart(cart);

    loadCart();
}
function removeItem(index) {

    let cart = getCart();

    cart.splice(index, 1);

    saveCart(cart);

    loadCart();
}
function parsePrice(price) {
    return Number(price.replace(/[^\d]/g, ""));
}