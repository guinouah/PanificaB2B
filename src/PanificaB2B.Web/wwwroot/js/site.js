// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

function reindexar(container) {
    container.querySelectorAll('[data-linha]').forEach((linha, indice) => {
        linha.querySelectorAll('[name]').forEach(campo => {
            campo.name = campo.name.replace(/\[\d+\]/, `[${indice}]`);
            campo.removeAttribute('id');
        });
    });
}
document.addEventListener('click', event => {
    const adicionar = event.target.closest('[data-adicionar]');
    if (adicionar) {
        const container = document.querySelector(`[data-linhas="${adicionar.dataset.adicionar}"]`);
        const template = document.getElementById(adicionar.dataset.template);
        const indice = container.querySelectorAll('[data-linha]').length;
        container.insertAdjacentHTML('beforeend', template.innerHTML.replaceAll('__i__', indice));
    }
    const remover = event.target.closest('[data-remover]');
    if (remover) {
        const container = remover.closest('[data-linhas]');
        remover.closest('[data-linha]').remove();
        reindexar(container);
    }
    if (event.target.closest('[data-imprimir]')) window.print();
});
document.querySelectorAll('[data-versao-destino]').forEach(select => {
    const destino = document.getElementById(select.dataset.versaoDestino);
    const atualizar = () => { destino.value = select.selectedOptions[0]?.dataset.versao || '0'; };
    select.addEventListener('change', atualizar);
    if (!destino.value || destino.value === '0') atualizar();
});

// Navegacao, busca e previa de atendimento.
const menuToggle = document.querySelector('[data-menu-toggle]');
const closeMenu = () => { document.body.classList.remove('nav-open'); menuToggle?.setAttribute('aria-expanded', 'false'); };
menuToggle?.addEventListener('click', () => { const opened = document.body.classList.toggle('nav-open'); menuToggle.setAttribute('aria-expanded', String(opened)); });
document.addEventListener('keydown', event => { if (event.key === 'Escape') closeMenu(); });
document.addEventListener('click', event => { if (document.body.classList.contains('nav-open') && !event.target.closest('.sidebar, [data-menu-toggle]')) closeMenu(); });
const normalizeSearch = value => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');
document.querySelectorAll('[data-table-search]').forEach(input => {
    const table = document.getElementById(input.dataset.tableSearch);
    const empty = document.querySelector(`[data-search-empty="${input.dataset.tableSearch}"]`);
    input.addEventListener('input', () => {
        const term = normalizeSearch(input.value.trim()); let visible = 0;
        table?.querySelectorAll('[data-search-row]').forEach(row => { row.hidden = !normalizeSearch(row.textContent).includes(term); if (!row.hidden) visible++; });
        if (empty) empty.style.display = visible === 0 && term ? 'block' : 'none';
    });
});
document.querySelectorAll('[data-cut-form]').forEach(form => {
    const rows = [...form.querySelectorAll('[data-cut-row]')];
    const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
    const quantity = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 });
    const reason = form.querySelector('[data-cut-reason]');
    const update = () => {
        let original = 0n, total = 0n, hasCut = false, exceedsStock = false, valid = true;
        rows.forEach(row => {
            const input = row.querySelector('[data-cut-quantity]');
            const requested = Math.round(Number(row.dataset.requested) * 1000);
            const entered = Number(input.value);
            const amount = Math.round(entered * 1000);
            const cents = BigInt(row.dataset.priceCents);
            const usable = input.value !== '' && Number.isFinite(entered) && amount >= 0 && amount <= requested && input.validity.valid;
            const removed = row.querySelector('[data-cut-removed]');
            original += (BigInt(requested) * cents + 500n) / 1000n;
            if (!usable) { valid = false; removed.textContent = '—'; return; }
            total += (BigInt(amount) * cents + 500n) / 1000n;
            removed.textContent = quantity.format((requested - amount) / 1000);
            removed.classList.toggle('has-cut', amount < requested);
            hasCut ||= amount < requested;
            exceedsStock ||= entered > Number(row.dataset.stock);
        });
        if (reason) { reason.required = hasCut; reason.setAttribute('aria-required', String(hasCut)); }
        form.querySelector('[data-cut-total]').textContent = valid ? money.format(Number(total) / 100) : '—';
        form.querySelector('[data-cut-discount]').textContent = valid ? money.format(Number(original - total) / 100) : '—';
        form.querySelector('[data-cut-warning]').textContent = !valid ? 'Confira as quantidades: use valores entre zero e o total pedido, com até três casas decimais.' : exceedsStock ? 'Há quantidade acima do saldo disponível. Ajuste o atendimento antes de confirmar.' : total === 0n ? 'Corte total: este pedido será encerrado sem venda e sem cobrança.' : hasCut ? 'Atendimento parcial. Informe o motivo do corte para continuar.' : 'Atendimento integral. Confira as quantidades antes de confirmar.';
    };
    form.addEventListener('input', update); update();
});

// Seletores de cadastros oficiais.
document.querySelectorAll('[data-uf-name]').forEach(select=>{
    const update=()=>{const name=select.closest('form').querySelector('input[name="Nome"]');if(name)name.value=select.selectedOptions[0]?.dataset.nome||'';};
    select.addEventListener('change',update);update();
});
const municipalitiesElement=document.getElementById('municipios-brasil');
if(municipalitiesElement){
    const municipalities=JSON.parse(municipalitiesElement.textContent);
    document.querySelectorAll('[data-municipio-uf]').forEach(state=>{
        const city=state.closest('form').querySelector('[data-municipio]');
        const update=(initial=false)=>{
            const selected=initial?city.dataset.selecionado:'';
            const uf=state.selectedOptions[0]?.dataset.uf;
            city.replaceChildren(new Option(uf?'Selecione o município':'Primeiro selecione o estado',''));
            (municipalities[uf]||[]).forEach(name=>city.add(new Option(name,name,false,name===selected)));
            city.disabled=!uf;
        };
        state.addEventListener('change',()=>update());update(true);
    });
}
document.querySelectorAll('[data-client-state]').forEach(state=>{
    const city=state.closest('form').querySelector('[data-client-city]');
    const options=[...city.options].filter(o=>o.value).map(o=>o.cloneNode(true));
    const update=(initial=false)=>{
        const previous=initial?city.value:'';
        city.replaceChildren(new Option(state.value?'Selecione a cidade':'Primeiro selecione o estado',''));
        options.filter(o=>o.dataset.estado===state.value).forEach(o=>{const option=o.cloneNode(true);option.selected=option.value===previous;city.add(option);});
        city.disabled=!state.value;
    };
    state.addEventListener('change',()=>update());update(true);
});
