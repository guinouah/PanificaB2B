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
