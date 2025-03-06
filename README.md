# Simple RAG as a service 

This a sample WIP rag project.

This RAG code interfaces with a Cosmos DB setup in azure. The Cosmos DB is used to store the vector data of embeddings.


This project has  a simple vueue.js front end project. The front end allows the user to upload a PDF embeddings to cosmos DB. The front also allows the user to ask questions in regards to the PDF against an LLM. 

## The project works as follows.

### Upload of File
-User uploads file.
-File is chunked and submitted to LLM to retrieve embeddings.
-Embeddings are store in a cosmos DB DB in azure. 

### Chat against PDF.
-The system prompt is setup such that the embeddings that are passed are used to determine the answer.
1. The user asks a question.
2. The question is used to extract relevant embeddings using cartesian lookup.
3. The embeddings are passed along with the question to the LLM (in this case chatgpt).
4. The LLM then provides an answer using the embeddings.



 ###  Enhancement in progress.

   -Google auth to allow multiple user to upload. Currently the db does have a user field but it is unused.
   -Allow user to select PDF files searched. Also save PDF file description.
   -Hybrid search and multi stage pipeline using graph and text search. Currently this design has the limitations of a normal vector rag




